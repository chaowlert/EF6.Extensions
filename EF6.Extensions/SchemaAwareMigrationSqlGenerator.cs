using System;
using System.Data.Entity.Migrations.Model;
using System.Data.Entity.SqlServer;
using System.Linq;

namespace EF6.Extensions
{
    public class SchemaAwareMigrationSqlGenerator : SqlServerMigrationSqlGenerator
    {
        private readonly string _schema;

        public SchemaAwareMigrationSqlGenerator(string schema)
        {
            _schema = schema;
        }

        protected override void Generate(AddColumnOperation addColumnOperation)
        {
            string newTableName = getNameWithReplacedSchema(addColumnOperation.Table);
            var newAddColumnOperation = new AddColumnOperation(newTableName, addColumnOperation.Column, addColumnOperation.AnonymousArguments);
            base.Generate(newAddColumnOperation);
        }

        protected override void Generate(AddPrimaryKeyOperation addPrimaryKeyOperation)
        {
            addPrimaryKeyOperation.Table = getNameWithReplacedSchema(addPrimaryKeyOperation.Table);
            base.Generate(addPrimaryKeyOperation);
        }

        protected override void Generate(AlterColumnOperation alterColumnOperation)
        {
            string tableName = getNameWithReplacedSchema(alterColumnOperation.Table);
            var newAlterColumnOperation = new AlterColumnOperation(tableName, alterColumnOperation.Column, alterColumnOperation.IsDestructiveChange);
            base.Generate(newAlterColumnOperation);
        }

        protected override void Generate(DropPrimaryKeyOperation dropPrimaryKeyOperation)
        {
            dropPrimaryKeyOperation.Table = getNameWithReplacedSchema(dropPrimaryKeyOperation.Table);
            base.Generate(dropPrimaryKeyOperation);
        }

        protected override void Generate(CreateIndexOperation createIndexOperation)
        {
            string name = getNameWithReplacedSchema(createIndexOperation.Table);
            createIndexOperation.Table = name;
            base.Generate(createIndexOperation);
        }

        protected override void Generate(CreateTableOperation createTableOperation)
        {
            string newTableName = getNameWithReplacedSchema(createTableOperation.Name);
            var newCreateTableOperation = new CreateTableOperation(newTableName, createTableOperation.AnonymousArguments)
            {
                PrimaryKey = createTableOperation.PrimaryKey
            };
            foreach (var column in createTableOperation.Columns)
            {
                newCreateTableOperation.Columns.Add(column);
            }

            base.Generate(newCreateTableOperation);
        }

        protected override void Generate(RenameTableOperation renameTableOperation)
        {
            string oldName = getNameWithReplacedSchema(renameTableOperation.Name);
            string newName = renameTableOperation.NewName.Split('.').Last();
            var newRenameTableOperation = new RenameTableOperation(oldName, newName, renameTableOperation.AnonymousArguments);
            base.Generate(newRenameTableOperation);
        }

        protected override void Generate(RenameIndexOperation renameIndexOperation)
        {
            string tableName = getNameWithReplacedSchema(renameIndexOperation.Table);
            var newRenameIndexOperation = new RenameIndexOperation(tableName, renameIndexOperation.Name, renameIndexOperation.NewName);
            base.Generate(newRenameIndexOperation);
        }

        protected override void Generate(AddForeignKeyOperation addForeignKeyOperation)
        {
            addForeignKeyOperation.DependentTable = getNameWithReplacedSchema(addForeignKeyOperation.DependentTable);
            addForeignKeyOperation.PrincipalTable = getNameWithReplacedSchema(addForeignKeyOperation.PrincipalTable);
            base.Generate(addForeignKeyOperation);
        }

        protected override void Generate(DropColumnOperation dropColumnOperation)
        {
            string newTableName = getNameWithReplacedSchema(dropColumnOperation.Table);
            var newDropColumnOperation = new DropColumnOperation(newTableName, dropColumnOperation.Name, dropColumnOperation.AnonymousArguments);
            base.Generate(newDropColumnOperation);
        }

        protected override void Generate(RenameColumnOperation renameColumnOperation)
        {
            string newTableName = getNameWithReplacedSchema(renameColumnOperation.Table);
            var newRenameColumnOperation = new RenameColumnOperation(newTableName, renameColumnOperation.Name, renameColumnOperation.NewName);
            base.Generate(newRenameColumnOperation);
        }

        protected override void Generate(DropTableOperation dropTableOperation)
        {
            string newTableName = getNameWithReplacedSchema(dropTableOperation.Name);
            var newDropTableOperation = new DropTableOperation(newTableName, dropTableOperation.AnonymousArguments);
            base.Generate(newDropTableOperation);
        }

        protected override void Generate(DropForeignKeyOperation dropForeignKeyOperation)
        {
            dropForeignKeyOperation.PrincipalTable = getNameWithReplacedSchema(dropForeignKeyOperation.PrincipalTable);
            dropForeignKeyOperation.DependentTable = getNameWithReplacedSchema(dropForeignKeyOperation.DependentTable);
            base.Generate(dropForeignKeyOperation);
        }

        protected override void Generate(DropIndexOperation dropIndexOperation)
        {
            dropIndexOperation.Table = getNameWithReplacedSchema(dropIndexOperation.Table);
            base.Generate(dropIndexOperation);
        }

        protected override void Generate(SqlOperation sqlOperation)
        {
            var sql = sqlOperation.Sql.Replace("dbo.", $"{_schema}.");
            var altered = new SqlOperation(sql, sqlOperation.AnonymousArguments)
            {
                SuppressTransaction = sqlOperation.SuppressTransaction
            };
            base.Generate(altered);
        }

        private string getNameWithReplacedSchema(string name)
        {
            string[] nameParts = name.Split('.');
            string newName;

            switch (nameParts.Length)
            {
                case 1:
                    newName = $"{_schema}.{nameParts[0]}";
                    break;

                case 2:
                    newName = $"{_schema}.{nameParts[1]}";
                    break;

                case 3:
                    newName = $"{_schema}.{nameParts[1]}.{nameParts[2]}";
                    break;

                default:
                    throw new NotSupportedException();
            }

            return newName;
        }
    }
}
