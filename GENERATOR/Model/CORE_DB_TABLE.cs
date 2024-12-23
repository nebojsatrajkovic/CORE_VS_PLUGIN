using System.Collections.Generic;

namespace CORE_VS_PLUGIN.GENERATOR.Model
{
    internal class CORE_DB_TABLE
    {
        public string Name { get; set; }
        public List<CORE_DB_TABLE_COLUMN> Columns { get; set; } = new List<CORE_DB_TABLE_COLUMN>();
    }

    internal class CORE_DB_TABLE_COLUMN
    {
        public string TableName { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int DataType { get; set; }
        public int OrdinalPosition { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
    }
}