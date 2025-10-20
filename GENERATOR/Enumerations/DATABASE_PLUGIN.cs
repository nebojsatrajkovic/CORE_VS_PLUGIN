using System.ComponentModel;

namespace CORE_VS_PLUGIN.GENERATOR.Enumerations
{
    public enum DATABASE_PLUGIN
    {
        [Description("MSSQL")]
        MSSQL,
        [Description("MySQL")]
        MySQL,
        [Description("PostgreSQL")]
        PostgreSQL
    }
}