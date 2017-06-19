using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Ewbi
{

    /*

      Copyright (c) 2007 E. W. Bachtal, Inc.

      Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
      and associated documentation files (the "Software"), to deal in the Software without restriction, 
      including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
      and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
      subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all copies or substantial 
        portions of the Software.

      The software is provided "as is", without warranty of any kind, express or implied, including but not 
      limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In 
      no event shall the authors or copyright holders be liable for any claim, damages or other liability, 
      whether in an action of contract, tort or otherwise, arising from, out of or in connection with the 
      software or the use or other dealings in the software. 

      --------------------------------------------------------------------------------------------------------

      FullTextSearch
    
      Parses and rewrites ad hoc SQL Server 2005 full-text search conditions into their valid normal form.  
      Always returns a valid full-text search condition suitable for use in a CONTAINS or CONTAINSTABLE query.  
      Exceptions can optionally be thrown when certain improper constructs are present.  If exceptions are not 
      thrown, the invalid constructs are replaced, removed, and/or massaged as needed to form a valid condition.
   
      See the following post for additional information:
   
      http://ewbi.blogs.com/develops/2007/05/normalizing_sql.html

      In the production version, eligible search terms are gathered into a collection of stemmed regex patterns 
      for matching in the resulting text for presentation, similar to the way Google highlights search terms in 
      its results.  The elided code uses UniqueList<> (http://ewbi.blogs.com/develops/2007/05/uniquelistt_for.html) 
      to gather unique terms using a SearchTerm class and stems terms and phrases using a C# implementation of the
      English Porter2 stemming algorithm (http://snowball.tartarus.org/otherlangs/english_cpp.txt) by Kamil Bartocha.  
      For simplicity, the code provided here simply gathers the terms into a List<string>, accessible as an array of 
      strings.  To learn more about these additional features, contact me at info@ewbi.com.
    
      v1.0 Original
    
    */

    [Flags]
    public enum FullTextSearchOptions
    {
        None = 0,
        Default = StemAll | TrimPrefixAll,
        StemAll = StemTerms | StemPhrases,
        TrimPrefixAll = TrimPrefixTerms | TrimPrefixPhrases,
        ThrowOnAll = ThrowOnUnbalancedParens | ThrowOnUnbalancedQuotes | ThrowOnInvalidNearUse,

        StemTerms = 1,                  // Apply FORMSOF(INFLECTIONAL) when not a prefix term or adjoining a NEAR.
        StemPhrases = 2,

        TrimPrefixTerms = 4,            // Trim prefix terms to first intra-word asterisk (leaving inner asterisks
        TrimPrefixPhrases = 8,          // will always result in no matches).

        ThrowOnUnbalancedParens = 128,  // Otherwise silently patches up and prevents under/overflow.
        ThrowOnUnbalancedQuotes = 256,  // Otherwise closes at end and assumes inner single instance quotes are intentional.
        ThrowOnInvalidNearUse = 512,    // Otherwise silently switches the bad NEARs to ANDs.
    }

    public sealed partial class FullTextSearch
    {
        private readonly List<string> _searchTerms;

        public FullTextSearch(string condition) : this(condition, FullTextSearchOptions.Default) { }

        public FullTextSearch(string condition, FullTextSearchOptions options)
        {

            Condition = condition;
            Options = options;

            var parser = new ConditionParser(condition, options);

            NormalForm = parser.RootExpression.ToString();

            _searchTerms = new List<string>();

            foreach (var exp in parser.RootExpression)
            {
                if (exp.IsSubexpression) continue;
                if (exp.Term.Length == 0) continue;
                _searchTerms.Add(exp.Term);
            }

        }

        public string Condition { get; }

        public FullTextSearchOptions Options { get; }

        public string NormalForm { get; }

        public string[] SearchTerms => _searchTerms.ToArray();


    }
    sealed class ConditionParser
    {

        private readonly FullTextSearchOptions _options;

        private StringBuilder _token;
        private ConditionOperator _lastOp;
        private readonly bool _inQuotes;

        private ConditionExpression _currentExpression;

        public ConditionParser(string condition, FullTextSearchOptions options)
        {
            var stream = new ConditionStream(condition, options);

            this._options = options;

            RootExpression = new ConditionExpression(options);
            _currentExpression = RootExpression;

            Reset();

            while (stream.Read())
            {
                if (ConditionOperator.IsSymbol(stream.Current))
                {
                    PutToken();
                    SetToken(stream.Current);
                    PutToken();
                    continue;
                }
                switch (stream.Current)
                {
                    case ' ': PutToken(); continue;
                    case '(': PushExpression(); continue;
                    case ')': PopExpression(); continue;
                    case '"':
                        PutToken();
                        _inQuotes = true;
                        SetToken(stream.ReadQuote());
                        PutToken();
                        _inQuotes = false;
                        continue;
                }
                AddToken(stream.Current);
            }
            PutToken();

            if (!ReferenceEquals(RootExpression, _currentExpression))
            {
                if ((options & FullTextSearchOptions.ThrowOnUnbalancedParens) != 0)
                {
                    throw new InvalidOperationException("Unbalanced parentheses.");
                }
            }

        }

        public ConditionExpression RootExpression { get; }

        private void Reset()
        {
            ResetToken();
            _lastOp = ConditionOperator.And;
        }

        private void ResetToken()
        {
            _token = new StringBuilder();
        }

        private void PushExpression()
        {
            PutToken();
            _currentExpression = _currentExpression.AddSubexpression(_lastOp);
        }

        private void PopExpression()
        {
            PutToken();
            if (_currentExpression.IsRoot)
            {
                if ((_options & FullTextSearchOptions.ThrowOnUnbalancedParens) != 0)
                {
                    throw new InvalidOperationException("Unbalanced parentheses.");
                }
            }
            else
            {
                _currentExpression = _currentExpression.Parent;
            }
            Reset();
        }

        private void AddToken(char c)
        {
            _token.Append(c);
        }

        private void SetToken(char c)
        {
            SetToken(c.ToString());
        }

        private void SetToken(string s)
        {
            _token = new StringBuilder(s);
        }

        private void PutToken()
        {

            // Check to see if the token is an operator.

            if (!_inQuotes && ConditionOperator.TryParse(_token.ToString(), ref _lastOp))
            {
                ResetToken();
                return;
            }

            // Not an operator, so it's a term.

            var term = _token.ToString();
            if (_inQuotes)
            {
                term = Regex.Replace(term.Trim(), @"[ ]{2,}", " ");
            }
            if (term.Length == 0 && !_inQuotes) return;

            _currentExpression.AddTerm(_lastOp, term);

            Reset();

        }

    }

    sealed class ConditionStream
    {

        private readonly FullTextSearchOptions _options;

        private readonly string _condition;
        private int _index;

        public ConditionStream(string condition, FullTextSearchOptions options)
        {
            this._options = options;
            this._condition = Regex.Replace(condition ?? string.Empty, @"\x09|\x0D|\x0A|[\x01-\x08]|\x10|[\x0B-\x0C]|[\x0E-\x1F]", " ");
            _index = -1;
        }

        public char Current => Eoq() || Boq() ? (char)0 : _condition[_index];

        public bool Read()
        {
            _index++;
            if (Eoq()) return false;
            return true;
        }

        public string ReadQuote()
        {
            var sb = new StringBuilder();
            while (Read())
            {
                if (Current.Equals('"'))
                {
                    if (_index + 1 == _condition.Length)
                    {
                        _index = _condition.Length;
                        return sb.ToString();
                    }
                    var peek = _condition[_index + 1];
                    if (peek == ' ' || peek == ')' || peek == '(' || ConditionOperator.IsSymbol(peek))
                    {
                        return sb.ToString();
                    }
                    if (peek == '"')
                    {
                        _index += 1;
                    }
                    else
                    {
                        if ((_options & FullTextSearchOptions.ThrowOnUnbalancedQuotes) != 0)
                        {
                            return sb.ToString();
                        }
                    }
                }
                sb.Append(Current);
            }
            if ((_options & FullTextSearchOptions.ThrowOnUnbalancedQuotes) != 0)
            {
                throw new InvalidOperationException("Unbalanced quotes.");
            }
            return sb.ToString();
        }

        private bool Boq()
        {
            return _index < 0;
        }

        private bool Eoq()
        {
            return _index >= _condition.Length;
        }

    }

    sealed class ConditionExpression : IEnumerable<ConditionExpression>
    {

        private readonly FullTextSearchOptions _options;
        private readonly int _index;
        private readonly List<ConditionExpression> _subexpressions;

        private ConditionExpression()
        {
            Term = string.Empty;
            _subexpressions = new List<ConditionExpression>();
        }

        public ConditionExpression(FullTextSearchOptions options)
            : this()
        {
            this._options = options;
        }

        private ConditionExpression(ConditionExpression parent, ConditionOperator op)
            : this(parent._options)
        {
            _index = parent._subexpressions.Count;
            this.Parent = parent;
            this.Operator = op;
        }

        private ConditionExpression(ConditionExpression parent, ConditionOperator op, string term)
            : this(parent, op)
        {

            this.Term = term;

            IsTerm = true;

            TermIsPhrase = term.IndexOf(' ') != -1;
            var prefixIndex = term.IndexOf('*');
            TermIsPrefix = prefixIndex != -1;

            if (!TermIsPrefix) return;

            if (!TermIsPhrase)
            {
                if ((_options & FullTextSearchOptions.TrimPrefixTerms) == 0) return;
                if (prefixIndex == term.Length - 1) return;
                this.Term = prefixIndex == 0 ? "" : term.Remove(prefixIndex + 1);
                return;
            }

            if ((_options & FullTextSearchOptions.TrimPrefixPhrases) == 0) return;
            term = Regex.Replace(term, @"(\*[^ ]+)|(\*)", "");
            term = Regex.Replace(term.Trim(), @"[ ]{2,}", " ");
            this.Term = term + "*";

        }

        public ConditionExpression Parent { get; }

        public bool IsRoot => Parent == null;

        public bool IsLastSubexpression => IsRoot || !IsRoot && _index == Parent._subexpressions.Count - 1;

        public ConditionExpression NextSubexpression => !IsLastSubexpression ? Parent._subexpressions[_index + 1] : null;

        public ConditionOperator Operator { get; }

        public bool IsTerm { get; }

        public bool TermIsPhrase { get; }

        public bool TermIsPrefix { get; }

        public bool IsSubexpression => !IsTerm;

        public bool HasSubexpressions => _subexpressions.Count > 0;

        public ConditionExpression LastSubexpression => HasSubexpressions ? _subexpressions[_subexpressions.Count - 1] : null;

        public ConditionExpression AddSubexpression(ConditionOperator op)
        {

            var newOp = op;
            if (op == ConditionOperator.Near)
            {
                if ((_options & FullTextSearchOptions.ThrowOnInvalidNearUse) != 0)
                {
                    throw new InvalidOperationException("Invalid near operator before subexpression.");
                }
                newOp = ConditionOperator.And;
            }

            var exp = new ConditionExpression(this, newOp);

            _subexpressions.Add(exp);

            return exp;

        }

        public void AddTerm(ConditionOperator op, string term)
        {

            if (!HasSubexpressions)
            {
                op = ConditionOperator.And;
            }
            else
            {
                if (op == ConditionOperator.Near)
                {
                    if (LastSubexpression.HasSubexpressions)
                    {
                        if ((_options & FullTextSearchOptions.ThrowOnInvalidNearUse) != 0)
                        {
                            throw new InvalidOperationException("Invalid near operator after subexpression.");
                        }
                        op = ConditionOperator.And;
                    }
                }
            }

            var exp = new ConditionExpression(this, op, term);

            _subexpressions.Add(exp);

        }

        public string Term { get; }

        public override string ToString()
        {

            var sb = new StringBuilder();

            if (IsTerm)
            {

                var doStem = DoStem();

                if (doStem) sb.Append("formsof(inflectional, ");

                sb.Append("\"");
                sb.Append(Term.Replace("\"", "\"\""));
                sb.Append("\"");

                if (doStem) sb.Append(")");

            }
            else
            {

                if (!IsRoot) sb.Append("(");

                if (!HasSubexpressions)
                {
                    sb.Append("\"\"");  // Want to avoid 'Null or empty full-text predicate' exception.
                }
                else
                {
                    for (var i = 0; i < _subexpressions.Count; i++)
                    {
                        var exp = _subexpressions[i];
                        if (i > 0)
                        {
                            sb.Append(" ");
                            sb.Append(exp.Operator);
                            sb.Append(" ");
                        }
                        sb.Append(exp);
                    }
                }

                if (!IsRoot) sb.Append(")");

            }

            return sb.ToString();

        }

        private bool DoStem()
        {

            if (IsSubexpression) return false;
            if (Term.Length < 2) return false;
            if (TermIsPrefix) return false;
            if (!TermIsPhrase && (_options & FullTextSearchOptions.StemTerms) == 0 || 
                TermIsPhrase && (_options & FullTextSearchOptions.StemPhrases) == 0) return false;
            if (Operator == ConditionOperator.Near) return false;
            if (!IsLastSubexpression && NextSubexpression.Operator == ConditionOperator.Near) return false;

            return true;

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ConditionExpression> GetEnumerator()
        {
            foreach (var exp in _subexpressions)
            {
                yield return exp;
                if (exp.HasSubexpressions)
                {
                    foreach (var exp2 in exp)
                    {
                        yield return exp2;
                    }
                }
            }
        }

    }

    struct ConditionOperator
    {

        private const char And1Symbol = '&';
        private const char And2Symbol = '+';
        private const char And3Symbol = ',';
        private const char And4Symbol = ';';
        private const char AndNot1Symbol = '-';
        private const char AndNot2Symbol = '!';
        private const char OrSymbol = '|';
        private const char NearSymbol = '~';

        private const int OpAnd = 0;
        private const int OpAndNot = 1;
        private const int OpOr = 2;
        private const int OpNear = 3;

        public static readonly ConditionOperator And = new ConditionOperator(OpAnd);
        public static readonly ConditionOperator AndNot = new ConditionOperator(OpAndNot);
        public static readonly ConditionOperator Or = new ConditionOperator(OpOr);
        public static readonly ConditionOperator Near = new ConditionOperator(OpNear);

        private readonly int _value;

        private ConditionOperator(int value)
        {
            this._value = value;
        }

        public override string ToString()
        {
            switch (_value)
            {
                case OpAndNot: return "and not";
                case OpOr: return "or";
                case OpNear: return "near";
                default:
                    return "and";
            }
        }

        public static bool IsSymbol(char symbol)
        {
            switch (symbol)
            {
                case And1Symbol: return true;
                case And2Symbol: return true;
                case And3Symbol: return true;
                case And4Symbol: return true;
                case AndNot1Symbol: return true;
                case AndNot2Symbol: return true;
                case OrSymbol: return true;
                case NearSymbol: return true;
            }
            return false;
        }

        public static bool TryParse(string s, ref ConditionOperator op)
        {

            if (s.Length == 1)
            {
                switch (s[0])
                {
                    case And1Symbol: goto case And4Symbol;
                    case And2Symbol: goto case And4Symbol;
                    case And3Symbol: goto case And4Symbol;
                    case And4Symbol:
                        op = And; return true;
                    case AndNot1Symbol:
                        op = AndNot; return true;
                    case AndNot2Symbol:
                        if (op != And) return false;
                        op = AndNot;
                        return true;
                    case OrSymbol:
                        op = Or; return true;
                    case NearSymbol:
                        op = Near; return true;
                }
                return false;
            }

            if (s.Equals(And.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                op = And;
                return true;
            }
            if (s.Equals("not", StringComparison.OrdinalIgnoreCase) && op == And)
            {
                op = AndNot;
                return true;
            }
            if (s.Equals(Or.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                op = Or;
                return true;
            }
            if (s.Equals(Near.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                op = Near;
                return true;
            }

            return false;

        }

        public static bool operator ==(ConditionOperator obj1, ConditionOperator obj2)
        {
            return obj1.Equals(obj2);
        }
        public static bool operator !=(ConditionOperator obj1, ConditionOperator obj2)
        {
            return !obj1.Equals(obj2);
        }
        public override bool Equals(object obj)
        {
            return obj is ConditionOperator && Equals((ConditionOperator)obj);
        }
        private bool Equals(ConditionOperator obj)
        {
            return _value == obj._value;
        }
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

    }

}