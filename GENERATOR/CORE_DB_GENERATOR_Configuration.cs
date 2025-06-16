namespace CORE_VS_PLUGIN.MSSQL_GENERATOR
{
    public class CORE_DB_GENERATOR_Configuration
    {
        public string ORM_Location { get; set; }
        public string ORM_Namespace { get; set; }
        public string ConnectionString { get; set; }
        public string TablesPrefix { get; set; }
    }

    public class CORE_DB_GENERATOR_PostgreSQL_Configuration : CORE_DB_GENERATOR_Configuration
    {
        public string PostgreSQL_Schema { get; set; }
    }
}