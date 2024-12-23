using System.ComponentModel;

namespace CORE_VS_PLUGIN.MSSQL_GENERATOR.Enumerations
{
    public enum CORE_DB_TABLE_TEMPLATE_PLACEHOLDER
    {
        [Description("${NAMESPACE}")]
        NAMESPACE,

        [Description("${TABLE_NAME}")]
        TABLE_NAME,

        [Description("${MODEL_ATTRIBUTES}")]
        MODEL_ATTRIBUTES,

        [Description("${QUERY_ATTRIBUTES}")]
        QUERY_ATTRIBUTES,

        [Description("${DB_TYPE}")]
        DB_TYPE,

        [Description("${PRIMARY_KEY_ATTRIBUTE}")]
        PRIMARY_KEY_ATTRIBUTE
    }
}