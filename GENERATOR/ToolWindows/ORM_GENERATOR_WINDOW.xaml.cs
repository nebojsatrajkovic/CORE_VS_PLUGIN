using CORE_VS_PLUGIN.GENERATOR;
using CORE_VS_PLUGIN.GENERATOR.Enumerations;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Windows;

namespace CORE_VS_PLUGIN.MSSQL_GENERATOR
{
    public partial class ORM_GENERATOR_WINDOW : DialogWindow
    {
        DATABASE_PLUGIN GENERATOR_PLUGIN;

        public ORM_GENERATOR_WINDOW(DATABASE_PLUGIN GENERATOR_PLUGIN)
        {
            this.GENERATOR_PLUGIN = GENERATOR_PLUGIN;

            InitializeComponent();

            var configurationPreviewObject = new CORE_DB_GENERATOR_Configuration { ConnectionString = string.Empty, ORM_Location = string.Empty, ORM_Namespace = string.Empty };

            lblConfigurationObjectPreview.Content = JsonConvert.SerializeObject(configurationPreviewObject, Formatting.Indented);
        }

        private void btnChooseConfigurationFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
                txtConfigurationFile.Text = openFileDialog.FileName;
        }

        private void btExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btExecute_Click(object sender, RoutedEventArgs e)
        {
            var isSuccess = false;

            if (GENERATOR_PLUGIN == DATABASE_PLUGIN.MSSQL)
            {
                isSuccess = CORE_MSSQL_DB_Generator.GenerateORMs_FromMSSQL(txtConfigurationFile.Text);
            }
            else if (GENERATOR_PLUGIN == DATABASE_PLUGIN.MySQL)
            {
                isSuccess = CORE_MySQL_DB_Generator.GenerateORMs_From_MySQL(txtConfigurationFile.Text);
            }
            else if (GENERATOR_PLUGIN == DATABASE_PLUGIN.PostgreSQL)
            {
                isSuccess = CORE_PostgreSQL_DB_Generator.GenerateORMs_From_PostgreSQL(txtConfigurationFile.Text);
            }

            var messageBoxText = isSuccess ? "Successfully generated ORM classes!" : "Failed to generate ORM classes!";
            var caption = "Operation result";
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBoxImage icon = isSuccess ? MessageBoxImage.Information : MessageBoxImage.Error;

            MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);

            Close();
        }
    }
}