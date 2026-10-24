using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using PKForge.Application;
using PKForge.Domain;

namespace PKForge.Linux.Workspace;

/// <summary>Controller/touch forms over explicit portable record types; no MAUI binding emulation.</summary>
public sealed class NativeForms(NativeDialogs dialogs)
{
    public IFileDialogService? Files { get; set; }
    public static string Name(string value) => System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ");
    public static string Display(object? value)
    {
        if (value is null) return "—";
        if (value is string text) return text;
        if (value is byte[] bytes) return $"{bytes.Length} bytes";
        if (value is IEnumerable list) return string.Join(", ", list.Cast<object?>().Take(12).Select(Display));
        if (value.GetType().IsPrimitive || value is decimal or Enum or DateTime or DateTimeOffset or DateOnly) return value.ToString() ?? "";
        return string.Join(" · ", value.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0).Take(5).Select(p => Name(p.Name) + ": " + Display(p.GetValue(value))));
    }
    public static string Describe(object? value)
    {
        if (value is null) return "Unavailable for this format.";
        if (value is string text) return text;
        if (value is IEnumerable sequence) return string.Join('\n', sequence.Cast<object?>().Select(Display));
        return string.Join('\n', value.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0).Select(p => Name(p.Name) + ": " + Display(p.GetValue(value))));
    }
    public async Task<object?> ValueAsync(string title, Type type, object? current, GameTextRule? textRule = null)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool))
        {
            var choice = await dialogs.MenuAsync(title, "Current: " + Display(current), "Yes", "No");
            return choice is null ? null : choice == "Yes";
        }
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type); var labels = values.Cast<object>().Select(Display).ToArray();
            var index = await dialogs.PickAsync(title, labels); return index is { } i ? values.GetValue(i) : null;
        }
        if (type == typeof(string))
        {
            var mode = Files is null ? "Type text" : await dialogs.MenuAsync(title, Display(current), "Type text", "Import UTF-8 text file");
            if (mode == "Import UTF-8 text file")
            {
                var file = (await Files!.PickAsync(new(FilePurpose.Text, title))).FirstOrDefault();
                var value = file is null ? null : await File.ReadAllTextAsync(file.DocumentId, Encoding.UTF8);
                if (value is not null && textRule?.Accepts(value) == false) { await dialogs.MessageAsync("Game text", $"The game cannot encode this text, or it exceeds {textRule.MaximumLength} characters."); return null; }
                return value;
            }
            return mode is null ? null : await dialogs.TextAsync(title, current?.ToString() ?? "", rule: textRule);
        }
        if (type.IsArray || type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            var element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            var existing = current is IEnumerable sequence ? sequence.Cast<object?>().ToArray() : new object?[6];
            var values = Array.CreateInstance(element, existing.Length);
            for (var i = 0; i < existing.Length; i++) values.SetValue(existing[i] ?? Activator.CreateInstance(element), i);
            while (true)
            {
                var labels = new[] { "Done" }.Concat(values.Cast<object?>().Select((v, i) => $"{i + 1}: {Display(v)}")).ToArray();
                var choice = await dialogs.MenuAsync(title, "Select an entry to change it.", labels);
                if (choice is null) return null;
                var at = Array.IndexOf(labels, choice) - 1; if (at < 0) return values;
                var edited = await ValueAsync(title + " · " + (at + 1), element, values.GetValue(at));
                if (edited is not null) values.SetValue(edited, at);
            }
        }
        if (type == typeof(DateOnly)) { var text = await dialogs.TextAsync(title + " yyyy-MM-dd", current?.ToString() ?? ""); return DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var date) ? date : null; }
        if (type.IsPrimitive || type == typeof(decimal))
        {
            while (true)
            {
                var text = await dialogs.TextAsync(title, current is null ? "0" : Convert.ToString(current, CultureInfo.InvariantCulture)!, "0123456789.-ABCDEFx");
                if (text is null) return null;
                try
                {
                    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ChangeType(Convert.ToUInt64(text[2..], 16), type, CultureInfo.InvariantCulture);
                    return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
                }
                catch (Exception error) when (error is FormatException or OverflowException or InvalidCastException) { await dialogs.MessageAsync("Invalid value", error.Message); }
            }
        }
        return await RecordAsync(title, type, current);
    }
    public async Task<T?> EditAsync<T>(string title, object? current, Func<string, bool>? enabled = null, Func<string, IReadOnlyList<NamedChoice>?>? choices = null, Func<string, GameTextRule?>? textRules = null) where T : class
        => (T?)await RecordAsync(title, typeof(T), current, true, enabled, choices, textRules);
    private async Task<object?> NamedArrayAsync(string title, Type type, object? current, IReadOnlyList<NamedChoice> choices)
    {
        var element = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
        var existing = current is IEnumerable sequence ? sequence.Cast<object?>().ToArray() : new object?[4];
        var values = Array.CreateInstance(element, existing.Length);
        for (var i = 0; i < existing.Length; i++) values.SetValue(existing[i] ?? Activator.CreateInstance(element), i);
        while (true)
        {
            var labels = new[] { "Done" }.Concat(values.Cast<object?>().Select((v, i) => $"{i + 1}: {choices.FirstOrDefault(c => c.Id == Convert.ToInt32(v))?.Name ?? Display(v)}")).ToArray();
            var at = await dialogs.PickAsync(title, labels); if (at is null) return null; if (at == 0) return values;
            var picked = await dialogs.PickAsync(title + " " + at, choices.Select(c => c.Name).ToArray());
            if (picked is { } index) values.SetValue(Convert.ChangeType(choices[index].Id, element, CultureInfo.InvariantCulture), at.Value - 1);
        }
    }
    public async Task<object?> RecordAsync(string title, Type type, object? current, bool partial = false,
        Func<string, bool>? enabled = null, Func<string, IReadOnlyList<NamedChoice>?>? choices = null, Func<string, GameTextRule?>? textRules = null)
    {
        var constructor = type.GetConstructors().Where(c => c.GetParameters().All(p => p.Name is not null)).OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        if (constructor is null) { await dialogs.MessageAsync(title, Describe(current)); return null; }
        var parameters = constructor.GetParameters();
        if (parameters.Length == 0)
        {
            var fields = type.GetProperties().Where(p => p.CanRead && p.SetMethod is not null && enabled?.Invoke(p.Name) != false).ToArray();
            var instance = constructor.Invoke([]);
            foreach (var field in fields) if (current is not null) field.SetValue(instance, field.GetValue(current));
            while (true)
            {
                var labels = new[] { "Apply changes" }.Concat(fields.Select(p => Name(p.Name) + ": " + Display(p.GetValue(instance)))).ToArray();
                var choice = await dialogs.MenuAsync(title, "Changes are staged until Apply.", labels);
                if (choice is null) return null;
                if (choice == labels[0]) return instance;
                var field = fields[Array.IndexOf(labels, choice) - 1];
                var value = await ValueAsync(Name(field.Name), field.PropertyType, field.GetValue(instance), textRules?.Invoke(field.Name));
                if (value is not null) field.SetValue(instance, value);
            }
        }
        var edited = new Dictionary<string, object?>();
        object? Read(ParameterInfo parameter) => current?.GetType().GetProperty(parameter.Name!, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)?.GetValue(current);
        while (true)
        {
            var fields = parameters.Where(p => enabled?.Invoke(p.Name!) != false).ToArray();
            var labels = new[] { "Apply changes" }.Concat(fields.Select(p => Name(p.Name!) + ": " + Display(edited.GetValueOrDefault(p.Name!, Read(p))))).ToArray();
            var choice = await dialogs.MenuAsync(title, edited.Count == 0 ? "Changes are staged until Apply." : $"{edited.Count} fields changed", labels);
            if (choice is null) return null;
            if (choice == labels[0])
            {
                if (edited.Count == 0 && partial) return null;
                var arguments = parameters.Select(p => edited.TryGetValue(p.Name!, out var value) ? value : partial && p.HasDefaultValue ? p.DefaultValue : Read(p) ?? (p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();
                return constructor.Invoke(arguments);
            }
            var parameter = fields[Array.IndexOf(labels, choice) - 1]; object? changed;
            if (choices?.Invoke(parameter.Name!) is { } entries)
            {
                if (parameter.ParameterType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(parameter.ParameterType))
                    changed = await NamedArrayAsync(Name(parameter.Name!), parameter.ParameterType, edited.GetValueOrDefault(parameter.Name!, Read(parameter)), entries);
                else
                {
                    var index = await dialogs.PickAsync(Name(parameter.Name!), entries.Select(e => e.Name).ToArray());
                    var target = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                    changed = index is { } i ? target.IsEnum ? Enum.ToObject(target, entries[i].Id) : Convert.ChangeType(entries[i].Id, target, CultureInfo.InvariantCulture) : null;
                }
            }
            else changed = await ValueAsync(Name(parameter.Name!), parameter.ParameterType, edited.GetValueOrDefault(parameter.Name!, Read(parameter)), textRules?.Invoke(parameter.Name!));
            if (changed is not null) edited[parameter.Name!] = changed;
        }
    }
}
