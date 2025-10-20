using CORE_VS_PLUGIN.GENERATOR.Enumerations;
using CORE_VS_PLUGIN.GENERATOR.Model;
using CORE_VS_PLUGIN.Utils;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CORE_VS_PLUGIN.GENERATOR.ToolWindows
{
    public partial class DATABASE_BROWSER : DialogWindow
    {
        public ObservableCollection<DB_DW_Database> Databases { get; set; } = new ObservableCollection<DB_DW_Database>();
        public ObservableCollection<DB_DW_DatabaseTable> DatabaseTables { get; set; } = new ObservableCollection<DB_DW_DatabaseTable>();

        string path { get; set; }
        string itemNamespace { get; set; }
        DTE dte { get; set; }
        AsyncPackage package { get; set; }

        public DATABASE_BROWSER(DTE dte, AsyncPackage package, string path, string itemNamespace)
        {
            this.dte = dte;
            this.package = package;
            this.path = path;
            this.itemNamespace = itemNamespace;

            InitializeComponent();

            DataContext = this;

            Databases.Add(new DB_DW_Database { Name = "MySQL database", ConnectionString = "Server=localhost;Database=core_db;Uid=root;Pwd=root;Port=3306", PluginType = DATABASE_PLUGIN.MySQL });

            CmbDatabase.SelectedValue = Databases[0];
        }

        void BtnSearch_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                DatabaseTables.Clear();

                var searchCriteria = TxtSearch.Text;

                if (string.IsNullOrEmpty(searchCriteria)) return; // TODO notify that search won't work if no text is entered

                var connectionString = (string)CmbDatabase.SelectedValue;

                var plugin = Databases[CmbDatabase.SelectedIndex].PluginType;

                List<DB_DW_DatabaseTable> databaseTables = default;

                if (plugin == DATABASE_PLUGIN.MySQL)
                {
                    databaseTables = MySQL_SearchDatabaseTables(connectionString, searchCriteria);
                }
                else if (plugin == DATABASE_PLUGIN.MSSQL)
                {
                    databaseTables = MSSQL_SearchDatabaseTables(connectionString, searchCriteria);
                }
                else if (plugin == DATABASE_PLUGIN.PostgreSQL)
                {
                    databaseTables = PostgreSQL_SearchDatabaseTables(connectionString, searchCriteria);
                }

                if (databaseTables != null && databaseTables.Count > 0)
                {
                    foreach (var databaseTable in databaseTables)
                    {
                        DatabaseTables.Add(databaseTable);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleWriter.Write(dte, ex.Message, nameof(DATABASE_BROWSER));
                ConsoleWriter.Write(dte, ex.StackTrace, nameof(DATABASE_BROWSER));

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    ConsoleWriter.Write(dte, ex.InnerException.Message, nameof(DATABASE_BROWSER));
                }

                package.ShowMessageBox(nameof(DATABASE_BROWSER), "Failed to execute search database tables. Please check Visual Studio output.", Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_CRITICAL);
            }
        }

        void BtnGenerate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var plugin = Databases[CmbDatabase.SelectedIndex].PluginType;

                if (plugin == DATABASE_PLUGIN.MySQL)
                {
                    GenerateORM_MySQL();
                }
                else if (plugin == DATABASE_PLUGIN.MSSQL)
                {
                    GenerateORM_MSSQL();
                }
                else if (plugin == DATABASE_PLUGIN.PostgreSQL)
                {
                    GenerateORM_PostgreSQL();
                }
            }
            catch (System.Exception ex)
            {
                ConsoleWriter.Write(dte, ex.Message, nameof(DATABASE_BROWSER));
                ConsoleWriter.Write(dte, ex.StackTrace, nameof(DATABASE_BROWSER));

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    ConsoleWriter.Write(dte, ex.InnerException.Message, nameof(DATABASE_BROWSER));
                }

                package.ShowMessageBox(nameof(DATABASE_BROWSER), "Failed to generate XML files. Please check Visual Studio output.", Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_CRITICAL);
            }
        }

        void BtnExit_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

        void GenerateORM_MySQL()
        {
            var connectionString = (string)CmbDatabase.SelectedValue;

            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    var tableNames = DatabaseTables.Where(x => x.IsSelected).Select(x => $"'{x.Name}'").ToList();

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
                            Columns = x.Value.GroupBy(c => c.Name).ToDictionary(c => c.Key, c => c.First()).Values.ToList()
                        }).ToList();

                    CORE_MySQL_DB_Generator.GenerateORM(tables, itemNamespace, path);
                }
            }
        }

        void GenerateORM_MSSQL()
        {

        }

        void GenerateORM_PostgreSQL()
        {

        }

        List<DB_DW_DatabaseTable> MySQL_SearchDatabaseTables(string connectionString, string searchCriteria)
        {
            var result = new List<DB_DW_DatabaseTable>();

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            var query_GetTables = $"SHOW TABLES LIKE '%{searchCriteria}%';";

                            using (var command_GetTables = new MySqlCommand(query_GetTables, connection, transaction))
                            {
                                using (var reader_GetTable = command_GetTables.ExecuteReader())
                                {
                                    while (reader_GetTable.Read())
                                    {
                                        var value = reader_GetTable.GetString(0);

                                        if (!string.IsNullOrEmpty(value))
                                        {
                                            result.Add(new DB_DW_DatabaseTable { Name = value });
                                        }
                                    }

                                    reader_GetTable.Close();
                                }
                            }
                        }
                        catch (System.Exception)
                        {
                            throw;
                        }
                        finally
                        {
                            transaction?.Rollback();

                            connection?.Close();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleWriter.Write(dte, ex.Message, nameof(DATABASE_BROWSER));
                ConsoleWriter.Write(dte, ex.StackTrace, nameof(DATABASE_BROWSER));

                if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    ConsoleWriter.Write(dte, ex.InnerException.Message, nameof(DATABASE_BROWSER));
                }

                package.ShowMessageBox(nameof(DATABASE_BROWSER), "Failed to search database tables. Please check Visual Studio output.", Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_CRITICAL);
            }

            return result;
        }

        List<DB_DW_DatabaseTable> MSSQL_SearchDatabaseTables(string connectionString, string searchCriteria)
        {
            // implement mssql search

            return default;
        }

        List<DB_DW_DatabaseTable> PostgreSQL_SearchDatabaseTables(string connectionString, string searchCriteria)
        {
            // implement postgresql search

            return default;
        }
    }

    public class DB_DW_DatabaseTable
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }

    public class DB_DW_Database
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public DATABASE_PLUGIN PluginType { get; set; }
    }
}