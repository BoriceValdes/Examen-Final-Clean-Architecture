
namespace Pim.Domain;

public record Typology(string Code, string Name, IReadOnlyCollection<FormField> Fields);
public record FormField(string Name, FieldKind Kind, bool Required);
public enum FieldKind { Text, Number, Boolean, Date }

public class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Ean { get; private set; }
    public string Name { get; private set; }
    public string TypologyCode { get; private set; }
    public Dictionary<string, object?> Attributes { get; } = new();

    public Product(string ean, string name, string typologyCode)
    {
        Ean = ean;
        Name = name;
        TypologyCode = typologyCode;
    }

    public void UpdateAttributes(Typology typology, IDictionary<string, object?> input)
    {
        // Validate against FormDefinition
        foreach (var field in typology.Fields)
        {
            if (field.Required && (!input.ContainsKey(field.Name) || input[field.Name] is null))
                throw new InvalidOperationException($"Missing required field: {field.Name}");

            if (input.TryGetValue(field.Name, out var value) && value is not null)
            {
                switch (field.Kind)
                {
                    case FieldKind.Text when value is not string:
                    case FieldKind.Number when value is not IConvertible:
                    case FieldKind.Boolean when value is not bool:
                    case FieldKind.Date when value is not DateTime:
                        throw new InvalidOperationException($"Invalid type for field: {field.Name}");
                }
                Attributes[field.Name] = value;
            }
        }
        // Allow additional non-specified attributes but they won't be validated here (extensible)
        foreach (var kv in input)
            if (!Attributes.ContainsKey(kv.Key)) Attributes[kv.Key] = kv.Value;
    }
}
