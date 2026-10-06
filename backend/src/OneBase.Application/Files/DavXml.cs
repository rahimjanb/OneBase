using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace OneBase.Application.Files;

/// <summary>Файл или папка в ответе PROPFIND.</summary>
public sealed record DavResource(
    string Href,
    string Name,
    bool IsCollection,
    long Length,
    string ContentType,
    DateTimeOffset Created,
    DateTimeOffset Modified,
    string ETag);

public enum PropfindMode
{
    AllProp,
    PropName,
    Prop,
}

public sealed record PropfindRequest(PropfindMode Mode, IReadOnlyList<XName> Props);

/// <summary>
/// XML WebDAV (RFC 4918) — столько, сколько нужно Проводнику Windows и Office: свойства файлов и папок, PROPPATCH (Windows пишет
/// время файла — принимается без сохранения), блокировки (LOCK/UNLOCK).
/// </summary>
public static class DavXml
{
    public static readonly XNamespace D = "DAV:";

    private static readonly XName[] Known =
    [
        D + "displayname", D + "resourcetype", D + "getcontentlength", D + "getcontenttype", D + "getlastmodified",
        D + "creationdate", D + "getetag", D + "supportedlock", D + "lockdiscovery",
    ];

    /// <summary>Тело PROPFIND: пусто или allprop — все свойства, propname — только имена, prop — перечисленные.</summary>
    public static PropfindRequest ParsePropfind(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new PropfindRequest(PropfindMode.AllProp, []);
        }

        var root = Load(body)?.Root;
        if (root is null || root.Name != D + "propfind")
        {
            return new PropfindRequest(PropfindMode.AllProp, []);
        }

        if (root.Element(D + "propname") is not null)
        {
            return new PropfindRequest(PropfindMode.PropName, []);
        }

        return root.Element(D + "prop") is { } prop
            ? new PropfindRequest(PropfindMode.Prop, prop.Elements().Select(e => e.Name).Distinct().ToList())
            : new PropfindRequest(PropfindMode.AllProp, []);
    }

    public static string MultiStatus(IEnumerable<DavResource> resources, PropfindRequest request)
    {
        var multistatus = new XElement(D + "multistatus", new XAttribute(XNamespace.Xmlns + "D", D));
        foreach (var resource in resources)
        {
            var response = new XElement(D + "response", new XElement(D + "href", resource.Href));
            var names = request.Mode == PropfindMode.Prop ? request.Props : Known.Where(n => Applies(n, resource)).ToList();
            var found = new XElement(D + "prop");
            var missing = new XElement(D + "prop");
            foreach (var name in names)
            {
                if (!Known.Contains(name) || !Applies(name, resource))
                {
                    missing.Add(new XElement(name));
                }
                else
                {
                    found.Add(request.Mode == PropfindMode.PropName ? new XElement(name) : Value(name, resource));
                }
            }

            if (found.HasElements || !missing.HasElements)
            {
                response.Add(PropStat(found, "HTTP/1.1 200 OK"));
            }

            if (missing.HasElements)
            {
                response.Add(PropStat(missing, "HTTP/1.1 404 Not Found"));
            }

            multistatus.Add(response);
        }

        return Serialize(multistatus);
    }

    /// <summary>PROPPATCH: каждое свойство из запроса — «200 OK» (Windows записывает время файла; хранить его не нужно).</summary>
    public static string PropPatchResponse(string href, string? body)
    {
        var props = new XElement(D + "prop");
        var root = string.IsNullOrWhiteSpace(body) ? null : Load(body)?.Root;
        foreach (var name in root?.Descendants(D + "prop").SelectMany(p => p.Elements()).Select(e => e.Name).Distinct() ?? [])
        {
            props.Add(new XElement(name));
        }

        return Serialize(new XElement(D + "multistatus", new XAttribute(XNamespace.Xmlns + "D", D),
            new XElement(D + "response", new XElement(D + "href", href), PropStat(props, "HTTP/1.1 200 OK"))));
    }

    /// <summary>Ответ LOCK: выданная блокировка (владелец — как в запросе).</summary>
    public static string LockResponse(string token, string href, string? requestBody, int timeoutSeconds, bool depthInfinity)
    {
        var owner = string.IsNullOrWhiteSpace(requestBody) ? null : Load(requestBody)?.Root?.Element(D + "owner");
        return Serialize(new XElement(D + "prop", new XAttribute(XNamespace.Xmlns + "D", D),
            new XElement(D + "lockdiscovery",
                new XElement(D + "activelock",
                    new XElement(D + "locktype", new XElement(D + "write")),
                    new XElement(D + "lockscope", new XElement(D + "exclusive")),
                    new XElement(D + "depth", depthInfinity ? "infinity" : "0"),
                    owner is null ? null : new XElement(D + "owner", owner.Nodes()),
                    new XElement(D + "timeout", $"Second-{timeoutSeconds}"),
                    new XElement(D + "locktoken", new XElement(D + "href", token)),
                    new XElement(D + "lockroot", new XElement(D + "href", href))))));
    }

    private static bool Applies(XName name, DavResource resource) =>
        !resource.IsCollection || (name != D + "getcontentlength" && name != D + "getcontenttype" && name != D + "getetag");

    private static XElement Value(XName name, DavResource r) => name.LocalName switch
    {
        "displayname" => new XElement(name, r.Name),
        "resourcetype" => new XElement(name, r.IsCollection ? new XElement(D + "collection") : null),
        "getcontentlength" => new XElement(name, r.Length.ToString(CultureInfo.InvariantCulture)),
        "getcontenttype" => new XElement(name, r.ContentType),
        "getlastmodified" => new XElement(name, r.Modified.UtcDateTime.ToString("R", CultureInfo.InvariantCulture)),
        "creationdate" => new XElement(name, r.Created.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
        "getetag" => new XElement(name, r.ETag),
        "supportedlock" => new XElement(name,
            new XElement(D + "lockentry",
                new XElement(D + "lockscope", new XElement(D + "exclusive")),
                new XElement(D + "locktype", new XElement(D + "write")))),
        _ => new XElement(name),
    };

    private static XElement PropStat(XElement prop, string status) => new(D + "propstat", prop, new XElement(D + "status", status));

    private static XDocument? Load(string xml)
    {
        try
        {
            // Без DTD и внешних сущностей: тело приходит от клиента.
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string Serialize(XElement root) =>
        new XDeclaration("1.0", "utf-8", null) + Environment.NewLine + root.ToString(SaveOptions.DisableFormatting);
}
