using CORE_VS_PLUGIN.GENERATOR.Enumerations;
using CORE_VS_PLUGIN.GENERATOR.Model;
using CORE_VS_PLUGIN.MSSQL_GENERATOR;
using CORE_VS_PLUGIN.MSSQL_GENERATOR.Enumerations;
using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CORE_VS_PLUGIN.GENERATOR
{
    // TODO improve loading the database tables and their columns - do the same as for the mysql generator (single db query)

    public static class CORE_PostgreSQL_DB_Generator
    {
        public static bool GenerateORMs_From_PostgreSQL(string configurationFilePath)
        {
            var isSuccess = false;

            #region load configuration

            var configurationJson = File.ReadAllText(configurationFilePath);

            var configuration = JsonConvert.DeserializeObject<CORE_DB_GENERATOR_PostgreSQL_Configuration>(configurationJson);

            try { Directory.Delete(configuration.ORM_Location, true); } catch (Exception) { }
            try { Directory.CreateDirectory(configuration.ORM_Location); } catch (Exception) { }

            string template;

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CORE_VS_PLUGIN.GENERATOR.Templates.DB_ORM_TEMPLATE.txt"))
            {
                using (var reader = new StreamReader(stream))
                {
                    template = reader.ReadToEnd();
                }
            }

            #endregion load configuration

            using (var connection = new NpgsqlConnection(configuration.ConnectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        #region load table names

                        var query_GetTables = $"SELECT * FROM information_schema.tables where table_schema = '{configuration.PostgreSQL_Schema}';";
                        var tableNames = new List<string>();

                        using (var command_GetTables = new NpgsqlCommand(query_GetTables, connection, transaction))
                        {
                            using (var reader_GetTable = command_GetTables.ExecuteReader())
                            {
                                while (reader_GetTable.Read())
                                {
                                    var value = reader_GetTable.GetString(reader_GetTable.GetOrdinal("table_name"));

                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        tableNames.Add(value);
                                    }
                                }

                                reader_GetTable.Close();
                            }
                        }

                        if (tableNames == null || !tableNames.Any()) { Console.WriteLine("No tables were found to generate ORMs"); return false; }

                        #endregion load table names

                        #region load tables data

                        var tables = new List<CORE_DB_TABLE>();

                        foreach (var tableName in tableNames)
                        {
                            var query_GetTableData = $"SELECT * FROM information_schema.columns WHERE table_schema = '{configuration.PostgreSQL_Schema}' AND table_name = '{tableName}'";

                            var table = new CORE_DB_TABLE
                            {
                                Name = tableName
                            };

                            using (var command_GetTableData = new NpgsqlCommand(query_GetTableData, connection, transaction))
                            {
                                using (var reader_GetTableData = command_GetTableData.ExecuteReader())
                                {
                                    while (reader_GetTableData.Read())
                                    {
                                        var ordinalPosition = reader_GetTableData.GetInt32(reader_GetTableData.GetOrdinal("ordinal_position"));

                                        table.Columns.Add(new CORE_DB_TABLE_COLUMN
                                        {
                                            Name = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("column_name")),
                                            TypeName = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("udt_name")),
                                            DataType = 0,
                                            IsNullable = reader_GetTableData.GetString(reader_GetTableData.GetOrdinal("is_nullable")) != "NO",
                                            OrdinalPosition = ordinalPosition,
                                            IsPrimaryKey = ordinalPosition == 1
                                        });
                                    }

                                    reader_GetTableData.Close();
                                }
                            }

                            if (table.Columns == null || !table.Columns.Any()) { continue; }

                            tables.Add(table);
                        }

                        if (tables == null || !tables.Any()) { Console.WriteLine("No table data was found to generate ORMs"); return false; }

                        #endregion load tables data

                        #region generate orm using template

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

                                        else if (IsValueType(cSharpType))
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
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.NAMESPACE.Description(), configuration.ORM_Namespace)
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.TABLE_NAME.Description(), table.Name)
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.MODEL_ATTRIBUTES.Description(), modelBuilder.ToString())
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.QUERY_ATTRIBUTES.Description(), queryBuilder.ToString())
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.PRIMARY_KEY_ATTRIBUTE.Description(), table.Columns[0].Name)
                                   .Replace(CORE_DB_TABLE_TEMPLATE_PLACEHOLDER.DB_TYPE.Description(), GENERATOR_PLUGIN.PostgreSQL.Description());

                                File.WriteAllText($"{configuration.ORM_Location}\\{table.Name}.cs", tableTemplate);
                            }
                        }

                        #endregion generate orm using template

                        Console.WriteLine($"{nameof(CORE_PostgreSQL_DB_Generator)}: Successfully generated classes!");

                        isSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{nameof(CORE_PostgreSQL_DB_Generator)}: Failed to generate classes!");
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

        static bool IsValueType(string cSharpType)
        {
            return
                cSharpType == "decimal" ||
                cSharpType == "double" ||
                cSharpType == "float" ||
                cSharpType == "int" ||
                cSharpType == "long" ||
                cSharpType == "DateTime" ||
                cSharpType == "bool";
        }

        static string GetCSharpType(string typeName)
        {
            switch (typeName)
            {
                case "uuid":
                    return "Guid";
                case "numeric":
                case "money":
                    return "decimal";
                case "float8":
                    return "double";
                case "float4":
                    return "float";
                case "int2":
                case "int4":
                    return "int";
                case "int8":
                    return "long";
                case "varchar":
                case "text":
                    return "string";
                case "timestamp":
                case "time":
                case "timez":
                case "date":
                    return "DateTime";
                case "bit":
                case "bool":
                    return "bool";
                case "bytea":
                    return "byte[]";
                case "interval":
                    return "TimeSpan";
                case "inet":
                    return "IPAddress";
                default:
                    return "object";
            }
        }
    }
}