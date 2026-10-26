using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MijnThuis.Integrations.CLI.Menus;
using Spectre.Console;
using Spectre.Console.Json;
using Spectre.Console.Rendering;

namespace MijnThuis.Integrations.CLI.Rendering;

/// <summary>
/// Renders the result of an integration call: scalars inline, objects as a property table,
/// collections as tables and nested structures as JSON.
/// </summary>
internal static class ResultRenderer
{
    private const int MaxCellLength = 80;

    private static readonly JsonSerializerOptions IndentedJson = CreateJsonOptions(indented: true);
    private static readonly JsonSerializerOptions CompactJson = CreateJsonOptions(indented: false);

    public static void Render(object? result)
    {
        switch (result)
        {
            case CommandCompleted:
                AnsiConsole.MarkupLine("[green]:check_mark_button: Done[/]");
                break;
            case null:
                AnsiConsole.MarkupLine("[grey](no result)[/]");
                break;
            case var scalar when IsScalar(scalar.GetType()):
                AnsiConsole.Write(new Columns(new Markup("Result:"), FormatScalar(scalar)).Collapse());
                AnsiConsole.WriteLine();
                break;
            case IEnumerable items:
                AnsiConsole.Write(CreateTable(items));
                break;
            default:
                RenderObject(result);
                break;
        }
    }

    private static void RenderObject(object value)
    {
        var properties = GetProperties(value.GetType());
        var table = new Table().RoundedBorder().AddColumn("[bold]Property[/]").AddColumn("[bold]Value[/]");
        var details = new List<IRenderable>();

        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(value);

            if (propertyValue is null || IsScalar(propertyValue.GetType()))
            {
                table.AddRow(new Text(property.Name), FormatScalar(propertyValue));
            }
            else if (propertyValue is IEnumerable items && IsTabular(property.PropertyType))
            {
                var itemTable = CreateTable(items);
                table.AddRow(new Text(property.Name), new Markup($"[grey]{itemTable.Rows.Count} item(s), see below[/]"));
                details.Add(itemTable.Title($"[bold]{Markup.Escape(property.Name)}[/]"));
            }
            else
            {
                table.AddRow(new Text(property.Name), new JsonText(JsonSerializer.Serialize(propertyValue, IndentedJson)));
            }
        }

        AnsiConsole.Write(table);
        foreach (var detail in details)
        {
            AnsiConsole.Write(detail);
        }
    }

    internal static Table CreateTable(IEnumerable items)
    {
        var list = items.Cast<object?>().ToList();
        var elementType = list.FirstOrDefault(x => x is not null)?.GetType();
        var table = new Table().RoundedBorder();

        if (elementType is null || list.Any(item => item is not null && IsScalar(item.GetType())))
        {
            table.AddColumn("[bold]Value[/]");
            foreach (var item in list)
            {
                table.AddRow(FormatCell(item));
            }
        }
        else
        {
            var rows = list.Select(item => item is null
                ? new Dictionary<string, object?>()
                : GetProperties(item.GetType()).ToDictionary(p => p.Name, p => p.GetValue(item))).ToList();
            var columns = rows.SelectMany(row => row.Keys).Distinct().ToList();
            foreach (var column in columns)
            {
                table.AddColumn($"[bold]{Markup.Escape(column)}[/]");
            }

            foreach (var row in rows)
            {
                table.AddRow(columns.Select(column => FormatCell(row.GetValueOrDefault(column))));
            }
        }

        table.Caption($"[grey]{list.Count} item(s)[/]");
        return table;
    }

    private static IRenderable FormatCell(object? value)
    {
        if (value is null || IsScalar(value.GetType()))
        {
            return FormatScalar(value);
        }

        var json = JsonSerializer.Serialize(value, CompactJson);
        return new Text(json.Length > MaxCellLength ? json[..(MaxCellLength - 1)] + "…" : json);
    }

    private static IRenderable FormatScalar(object? value) => value switch
    {
        null => new Markup("[grey]-[/]"),
        bool b => new Markup(b ? "[green]yes[/]" : "[red]no[/]"),
        DateTime d => new Text(d.ToString(d.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        DateTimeOffset d => new Text(d.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)),
        decimal or double or float => new Text(Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture)),
        IFormattable f => new Text(f.ToString(null, CultureInfo.InvariantCulture)),
        _ => new Text(value.ToString() ?? string.Empty)
    };

    private static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime)
            || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(Guid);
    }

    private static bool IsTabular(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }

        var elementType = type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type).FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];

        // Rows with only scalar columns render nicely; anything more nested is shown as JSON.
        return elementType is not null && (IsScalar(elementType) || GetProperties(elementType).All(p => IsScalar(p.PropertyType)));
    }

    private static PropertyInfo[] GetProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).ToArray();

    private static JsonSerializerOptions CreateJsonOptions(bool indented) => new()
    {
        WriteIndented = indented,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}
