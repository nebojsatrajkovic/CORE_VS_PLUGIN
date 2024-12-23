// using System.Xml.Serialization;
// XmlSerializer serializer = new XmlSerializer(typeof(CORE_DB_QUERY_XML_Template));
// using (StringReader reader = new StringReader(xml))
// {
//    var test = (CORE_DB_QUERY_XML_Template)serializer.Deserialize(reader);
// }

using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

[XmlRoot(ElementName = "Template")]
public class CORE_DB_QUERY_XML_Template
{

    [XmlElement(ElementName = "Meta")]
    public CORE_DB_QUERY_XML_Meta Meta { get; set; }

    [XmlElement(ElementName = "SQL")]
    public string SQL { get; set; }

    [XmlElement(ElementName = "Parameter")]
    public CORE_DB_QUERY_XML_Parameter Parameter { get; set; }

    [XmlElement(ElementName = "Result")]
    public CORE_DB_QUERY_XML_Result Result { get; set; }
}

[XmlRoot(ElementName = "Meta")]
public class CORE_DB_QUERY_XML_Meta
{

    [XmlAttribute(AttributeName = "Method_Namespace")]
    public string MethodNamespace { get; set; }

    [XmlAttribute(AttributeName = "Method_ClassName")]
    public string MethodClassName { get; set; }
}

[XmlRoot(ElementName = "Parameter")]
public class CORE_DB_QUERY_XML_Parameter
{

    [XmlElement(ElementName = "ClassMember")]
    public List<CORE_DB_QUERY_XML_ClassMember> ClassMember { get; set; }

    [XmlAttribute(AttributeName = "ClassName")]
    public string ClassName { get; set; }
}

[XmlRoot(ElementName = "ResultClass")]
public class CORE_DB_QUERY_XML_ResultClass
{

    [XmlElement(ElementName = "ClassMember")]
    public List<CORE_DB_QUERY_XML_ClassMember> ClassMember { get; set; }

    [XmlAttribute(AttributeName = "Name")]
    public string Name { get; set; }

    [XmlAttribute(AttributeName = "IsCollection")]
    public bool IsCollection { get; set; }

    [XmlAttribute(AttributeName = "GroupBy")]
    public string GroupBy { get; set; }


    public List<CORE_DB_QUERY_XML_ClassMember> GetAllClassMembers()
    {
        var allClassMembers = new List<CORE_DB_QUERY_XML_ClassMember>();
        allClassMembers.AddRange(ClassMember.Where(x => !x.IsClass).ToList());

        foreach (var item in ClassMember.Where(x => x.IsClass).ToList())
        {
            CollectClassMembers(item, allClassMembers);
        }

        return allClassMembers;
    }

    private static void CollectClassMembers(CORE_DB_QUERY_XML_ClassMember classMember, List<CORE_DB_QUERY_XML_ClassMember> allClassMembers)
    {
        if (classMember.ClassMembers != null)
        {
            allClassMembers.AddRange(classMember.ClassMembers.Where(x => !x.IsClass).ToList());

            foreach (var nestedClassMember in classMember.ClassMembers.Where(x => x.IsClass).ToList())
            {
                CollectClassMembers(nestedClassMember, allClassMembers);
            }
        }
    }
}

[XmlRoot(ElementName = "ClassMember")]
public class CORE_DB_QUERY_XML_ClassMember
{

    [XmlAttribute(AttributeName = "Name")]
    public string Name { get; set; }

    [XmlAttribute(AttributeName = "Type")]
    public string Type { get; set; }

    [XmlAttribute(AttributeName = "IsCollection")]
    public bool IsCollection { get; set; }

    [XmlElement(ElementName = "ClassMember")]
    public List<CORE_DB_QUERY_XML_ClassMember> ClassMembers { get; set; }

    [XmlAttribute(AttributeName = "IsClass")]
    public bool IsClass { get; set; }

    [XmlAttribute(AttributeName = "GroupBy")]
    public string GroupBy { get; set; }

    public List<CORE_DB_QUERY_XML_ClassMember> GetAllClassMembers()
    {
        return new List<CORE_DB_QUERY_XML_ClassMember> { this }.Concat(ClassMembers.SelectMany(x => x.GetAllClassMembers())).ToList();
    }
}

[XmlRoot(ElementName = "Result")]
public class CORE_DB_QUERY_XML_Result
{

    [XmlElement(ElementName = "ResultClass")]
    public CORE_DB_QUERY_XML_ResultClass ResultClass { get; set; }
}