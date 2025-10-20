using CORE_VS_PLUGIN.GENERATOR.Enumerations;
using CORE_VS_PLUGIN.GENERATOR.Model;
using CORE_VS_PLUGIN.MSSQL_GENERATOR;
using CORE_VS_PLUGIN.MSSQL_GENERATOR.Enumerations;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CORE_VS_PLUGIN.GENERATOR
{
    public static class CORE_MySQL_DB_Generator
    {
        public static bool GenerateORMs_From_MySQL(string configurationFilePath)
        {
            var isSuccess = false;

            #region load configuration

            var configurationJson = File.ReadAllText(configurationFilePath);

            var configuration = JsonConvert.DeserializeObject<CORE_DB_GENERATOR_Configuration>(configurationJson);

            try { Directory.Delete(configuration.ORM_Location, true); } catch (Exception) { }
            try { Directory.CreateDirectory(configuration.ORM_Location); } catch (Exception) { }

            #endregion load configuration

            using (var connection = new MySqlConnection(configuration.ConnectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        #region load table names

                        var query_GetTables = "SHOW TABLES;";
                        var tableNames = new List<string>();

                        using (var command_GetTables = new MySqlCommand(query_GetTables, connection, transaction))
                        {
                            using (var reader_GetTable = command_GetTables.ExecuteReader())
                            {
                                while (reader_GetTable.Read())
                                {
                                    var value = reader_GetTable.GetString(0);

                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        if (string.IsNullOrEmpty(configuration.TablesPrefix) || value.StartsWith(configuration.TablesPrefix, StringComparison.OrdinalIgnoreCase))
                                        {
                                            tableNames.Add($"'{value}'");
                                        }
                                    }
                                }

                                reader_GetTable.Close();
                            }
                        }

                        if (tableNames == null || !tableNames.Any()) { Console.WriteLine("No tables were found to generate ORMs"); return false; }

                        #endregion load table names

                        #region load tables data

                        var dbTablesColumns = new List<CORE_DB_TABLE_COLUMN>();

                        var tableNamesParameter = string.Join(", ", tableNames);

                        var query_GetTableData = $"SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE table_name IN ({tableNamesParameter});";

                        using (var command_GetTableData = new MySqlCommand(query_GetTableData, connection, transaction))
                        {
                            using (var reader_GetTableData = command_GetTableData.ExecuteReader())
                            {
                                while (reader_GetTableData.Read())
                                {
                                    dbTablesColumns.Add(new CORE_DB_TABLE_COLUMN
                                    {
                                        Name = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("COLUMN_NAME")),
                                        TypeName = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("DATA_TYPE")),
                                        DataType = 0,
                                        IsNullable = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("IS_NULLABLE")) != "NO",
                                        OrdinalPosition = reader_GetTableData.GetInt32(reader_GetTableData.GetOrdinal("ORDINAL_POSITION")),
                                        IsPrimaryKey = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("COLUMN_KEY")) == "PRI",
                                        TableName = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("TABLE_NAME"))
                                    });
                                }

                                reader_GetTableData.Close();
                            }
                        }

                        var tables = dbTablesColumns.GroupBy(x => x.TableName).ToDictionary(x => x.Key, x => x.ToList())
                            .Select(x => new CORE_DB_TABLE
                            {
                                Name = x.Key,
                                Columns = x.Value.ToList()
                            }).ToList();

                        if (tables == null || !tables.Any()) { Console.WriteLine("No table data was found to generate ORMs"); return false; }

                        #endregion load tables data

                        GenerateORM(tables, configuration.ORM_Namespace, configuration.ORM_Location);

                        Console.WriteLine($"{nameof(CORE_MySQL_DB_Generator)}: Successfully generated classes!");

                        isSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{nameof(CORE_MySQL_DB_Generator)}: Failed to generate classes!");
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        transaction?.Rollback();

                        connection?.Close();
                    }
                }
            }

            return isSuccess;
        }

        static bool IsNotNullableType(string cSharpType)
        {
            return
                cSharpType == "decimal" ||
                cSharpType == "datetime" ||
                cSharpType == "guid" ||
                cSharpType == "double" ||
                cSharpType == "float" ||
                cSharpType == "int" ||
                cSharpType == "bool";
        }

        static string GetCSharpType(string typeName)
        {
            switch (typeName)
            {
                case "binary":
                    return "Guid";

                case "decimal":
                    return "decimal";

                case "double":
                    return "double";

                case "float":
                    return "float";

                case "bigint":
                case "int":
                case "smallint":
                    return "int";

                case "longtext":
                case "text":
                case "mediumtext":
                case "json":
                case "char":
                case "nchar":
                case "varchar":
                case "nvarchar":
                    return "string";

                case "timestamp":
                case "datetime":
                case "time":
                    return "DateTime";

                case "tinyint":
                    return "bool";

                default:
                    return "object";
            }
        }

        internal static void GenerateORM(List<CORE_DB_TABLE> tables, string classNamespace, string directory)
        {
            string template;

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.DB_ORM_TEMPLATE.txt"))
            {
                using (var reader = new StreamReader(stream))
                {
                    template = reader.ReadToEnd();
                }
            }

            foreach (var table in tables)
            {
                if (table.Columns != null && table.Columns.Any())
                {
                    var modelBuilder = new StringBuilder();
                    var queryBuilder = new StringBuilder();

                    foreach (var column in table.Columns)
                    {
                        var cSharpType = GetCSharpType(column.TypeName);

                        if (column.IsNullable)
                        {
                            modelBuilder.AppendLine($"        public {cSharpType}? {column.Name} {{ get; set; }}");
                        }
                        else
                        {
                            if (column.IsPrimaryKey && cSharpType == "Guid")
                            {
                                modelBuilder.AppendLine($"        public Guid {column.Name} {{ get; set; }} = Guid.NewGuid();");
                            }

                            else if (IsNotNullableType(cSharpType.ToLower()))
                            {
                                modelBuilder.AppendLine($"        public {cSharpType} {column.Name} {{ get; set; }}");
                            }
                            else
                            {
                                modelBuilder.AppendLine($"        public {cSharpType} {column.Name} {{ get; set; }} = null!;");
                            }
                        }

                        queryBuilder.AppendLine($"        public {cSharpType}? {column.Name} {{ get; set; }} = null;");
                    }

                    var tableTemplate = template
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.NAMESPACE.Description(), classNamespace)
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.TABLE_NAME.Description(), table.Name)
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.MODEL_ATTRIBUTES.Description(), modelBuilder.ToString())
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.QUERY_ATTRIBUTES.Description(), queryBuilder.ToString())
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.PRIMARY_KEY_ATTRIBUTE.Description(), table.Columns[0].Name)
                       .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.DB_TYPE.Description(), GENERATOR_PLUGIN.MySQL.Description());

                    File.WriteAllText($"{directory}\\{table.Name}.cs", tableTemplate);
                }
            }
        }
    }
}