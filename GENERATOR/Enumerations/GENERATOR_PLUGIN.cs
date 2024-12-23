using System.ComponentModel;

namespace CORE_VS_PLUGIN.GENERATOR.Enumerations
{
    public enum GENERATOR_PLUGIN
    {
        [Description("MSSQL")]
        MSSQL,
        [Description("MySQL")]
        MySQL,
        [Description("PostgreSQL")]
        PostgreSQL
    }
}