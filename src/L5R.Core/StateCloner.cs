using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace L5R.Core;

// Issue #863 (engine profile): MatchEngine.Copy used to be JsonSerializer.Deserialize(JsonSerializer.Serialize(x)), which was ~76% of an Execute
// and one full round trip per MatchEngine.State read. This is a deep cloner that produces exactly what that round trip produced, without the text.
//
// It does not re-implement the serializer's rules: it reads System.Text.Json's own resolved contract (JsonSerializerOptions.Default.GetTypeInfo)
// for each DECLARED type, the same contract the round trip used, and copies along it:
//   * an object copies the properties the serializer would write (getter present, [JsonIgnore] Always/WhenWritingNull/WhenWritingDefault and any
//     ShouldSerialize honoured) into the members it would read back: constructor parameters (a parameter whose property was not written gets its
//     declared default, as when the JSON lacks it), then settable/init properties; get-only and private-set members keep their initializer values,
//     and private state is never copied -- exactly as after deserialisation.
//   * a collection or dictionary is rebuilt as the type the deserializer would create for the declared type (List<T> for IReadOnlyList<T> etc.,
//     a default-comparer HashSet/Dictionary), in enumeration order.
//   * an immutable leaf (primitive, string, enum, decimal, Guid and their Nullable forms) is shared; nothing else is.
// Anything it does not recognise (custom converters, other collection kinds, object-typed members, required members, ...) falls back to the JSON
// round trip for that value with its declared type, so an unfamiliar type can only be slower, never different.
// StateClonerTests compares this against the JSON round trip at every step of seeded games (MatchEngine.CopyViaJson).
internal static class StateCloner
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;
    private static readonly ConcurrentDictionary<Type, Node> Nodes = new();
    private static readonly object BuildLock = new();
    private static Dictionary<Type, Node>? _building;
    // The JSON round trip refuses a graph deeper than its MaxDepth (64); the copy has the same guard instead of overflowing the stack on a cycle.
    private const int MaxDepth = 64;

    internal static T Clone<T>(T value) => value is null ? value : (T)For(typeof(T)).Clone(value, 0)!;

    /// <summary>The declared types this cloner hands to the JSON round trip (for tests and diagnosis).</summary>
    internal static IEnumerable<Type> FallbackTypes() => Nodes.Where(n => n.Value is JsonNode).Select(n => n.Key);

    internal static Node For(Type type)
    {
        if (Nodes.TryGetValue(type, out var ready)) return ready;
        lock (BuildLock)
        {
            if (Nodes.TryGetValue(type, out ready)) return ready;
            var outermost = _building is null;
            _building ??= [];
            try
            {
                if (_building.TryGetValue(type, out var pending)) return pending;
                var node = Create(type);
                _building[type] = node; // registered before Init, so a recursive type finds itself
                node.Init();
                if (outermost) foreach (var (t, n) in _building) Nodes.TryAdd(t, n);
                return node;
            }
            finally { if (outermost) _building = null; }
        }
    }

    private static readonly HashSet<Type> Leaves = [typeof(string), typeof(decimal), typeof(Guid)];
    private static bool IsLeaf(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || Leaves.Contains(u);
    }

    private static Node Create(Type type)
    {
        if (IsLeaf(type)) return new LeafNode(type);
        JsonTypeInfo info;
        try { info = Options.GetTypeInfo(type); }
        catch (Exception) { return new JsonNode(type); }
        if (info.Converter.GetType().Assembly != typeof(JsonSerializer).Assembly || type.GetCustomAttribute<JsonConverterAttribute>() is not null) return new JsonNode(type);
        switch (info.Kind)
        {
            case JsonTypeInfoKind.Object:
                return new ObjectNode(type, info);
            case JsonTypeInfoKind.Enumerable when info.ElementType is { } element:
                if (type.IsArray && type.GetArrayRank() == 1) return Generic(typeof(ArrayNode<>), type, element);
                if (type.IsGenericType)
                {
                    var def = type.GetGenericTypeDefinition();
                    if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(ICollection<>) || def == typeof(IEnumerable<>) || def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>))
                        return Generic(typeof(ListNode<>), type, element);
                    if (def == typeof(HashSet<>) || def == typeof(ISet<>) || def == typeof(IReadOnlySet<>))
                        return Generic(typeof(HashSetNode<>), type, element);
                }
                return new JsonNode(type);
            case JsonTypeInfoKind.Dictionary when info.KeyType is { } key && info.ElementType is { } value && IsLeaf(key) && !key.IsGenericType:
                if (type.IsGenericType)
                {
                    var def = type.GetGenericTypeDefinition();
                    if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                        return (Node)Activator.CreateInstance(typeof(DictionaryNode<,>).MakeGenericType(key, value), type)!;
                }
                return new JsonNode(type);
            default:
                return new JsonNode(type);
        }
    }

    private static Node Generic(Type nodeType, Type type, Type element) => (Node)Activator.CreateInstance(nodeType.MakeGenericType(element), type)!;

    internal abstract class Node(Type type)
    {
        public Type Type { get; } = type;
        public virtual void Init() { }
        public abstract object? Clone(object? value, int depth);
        protected static void Deeper(int depth)
        {
            if (depth > MaxDepth) throw new JsonException($"The state graph is deeper than {MaxDepth} levels (a cycle?); the JSON copy refuses it too.");
        }
    }

    private sealed class LeafNode(Type type) : Node(type)
    {
        public override object? Clone(object? value, int depth) => value;
    }

    private sealed class JsonNode(Type type) : Node(type)
    {
        public override object? Clone(object? value, int depth) => JsonSerializer.Deserialize(JsonSerializer.Serialize(value, Type, Options), Type, Options);
    }

    private sealed class ArrayNode<T>(Type type) : Node(type)
    {
        private Node _element = null!;
        private bool _leaf;
        public override void Init() { _element = For(typeof(T)); _leaf = _element is LeafNode; }
        public override object? Clone(object? value, int depth)
        {
            if (value is null) return null;
            var source = (T[])value;
            if (_leaf) return source.Clone();
            Deeper(depth);
            var copy = new T[source.Length];
            for (var i = 0; i < source.Length; i++) copy[i] = (T)_element.Clone(source[i], depth + 1)!;
            return copy;
        }
    }

    private sealed class ListNode<T>(Type type) : Node(type)
    {
        private Node _element = null!;
        private bool _leaf;
        public override void Init() { _element = For(typeof(T)); _leaf = _element is LeafNode; }
        public override object? Clone(object? value, int depth)
        {
            if (value is null) return null;
            Deeper(depth);
            if (value is List<T> list)
            {
                if (_leaf) return new List<T>(list);
                var copy = new List<T>(list.Count);
                foreach (var item in list) copy.Add((T)_element.Clone(item, depth + 1)!);
                return copy;
            }
            var result = value is ICollection<T> c ? new List<T>(c.Count) : [];
            foreach (var item in (IEnumerable<T>)value) result.Add(_leaf ? item : (T)_element.Clone(item, depth + 1)!);
            return result;
        }
    }

    private sealed class HashSetNode<T>(Type type) : Node(type)
    {
        private Node _element = null!;
        private bool _leaf;
        public override void Init() { _element = For(typeof(T)); _leaf = _element is LeafNode; }
        public override object? Clone(object? value, int depth)
        {
            if (value is null) return null;
            Deeper(depth);
            var copy = new HashSet<T>(); // the deserializer's set has the default comparer, whatever the original had
            foreach (var item in (IEnumerable<T>)value) copy.Add(_leaf ? item : (T)_element.Clone(item, depth + 1)!);
            return copy;
        }
    }

    private sealed class DictionaryNode<TKey, TValue>(Type type) : Node(type) where TKey : notnull
    {
        private Node _value = null!;
        private bool _leaf;
        public override void Init() { _value = For(typeof(TValue)); _leaf = _value is LeafNode; }
        public override object? Clone(object? value, int depth)
        {
            if (value is null) return null;
            Deeper(depth);
            var copy = value is ICollection<KeyValuePair<TKey, TValue>> c ? new Dictionary<TKey, TValue>(c.Count) : []; // default comparer, as deserialised
            foreach (var (k, v) in (IEnumerable<KeyValuePair<TKey, TValue>>)value) copy[k] = _leaf ? v : (TValue)_value.Clone(v, depth + 1)!;
            return copy;
        }
    }

    private sealed class ObjectNode(Type type, JsonTypeInfo info) : Node(type)
    {
        private sealed record Member(Func<object, object?> Get, Func<object, object?, bool>? ShouldSerialize, JsonIgnoreCondition Condition, object? DefaultValue,
            Node Value, Action<object, object?>? Set, int Parameter);
        private Member[] _members = [];
        private Func<object>? _create;
        private Func<object?[], object>? _construct;
        private object?[] _parameterDefaults = [];
        private Node? _fallback;

        public override void Init()
        {
            if (info.Properties.Any(p => p.IsRequired || p.CustomConverter is not null || p.IsExtensionData) || info.PolymorphismOptions is not null)
            {
                _fallback = new JsonNode(Type);
                return;
            }
            if (info.CreateObject is { } create) _create = create;
            else if (info.ConstructorAttributeProvider is ConstructorInfo ctor)
            {
                var parameters = ctor.GetParameters();
                _parameterDefaults = parameters.Select(p => p.HasDefaultValue && p.DefaultValue is not DBNull ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();
                foreach (var p in info.Properties) // the serializer's own view of each parameter's default wins where it has one
                    if (p.AssociatedParameter is { HasDefaultValue: true } a) _parameterDefaults[a.Position] = a.DefaultValue;
                var args = Expression.Parameter(typeof(object?[]), "args");
                var call = Expression.New(ctor, parameters.Select((p, i) => (Expression)Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), p.ParameterType)));
                _construct = Expression.Lambda<Func<object?[], object>>(Expression.Convert(call, typeof(object)), args).Compile();
            }
            else
            {
                _fallback = new JsonNode(Type); // nothing the deserializer could build either: let it say so
                return;
            }
            var members = new List<Member>();
            foreach (var p in info.Properties)
            {
                if (p.Get is null) continue; // never written
                var ignore = (p.AttributeProvider as MemberInfo)?.GetCustomAttribute<JsonIgnoreAttribute>(true);
                var condition = ignore?.Condition ?? JsonIgnoreCondition.Never;
                if (condition == JsonIgnoreCondition.Always || condition == JsonIgnoreCondition.WhenReading) continue;
                var parameter = p.AssociatedParameter?.Position ?? -1;
                var set = parameter < 0 ? p.Set : null;
                if (parameter < 0 && set is null) continue; // written but never read back (get-only / private set): the copy keeps the initializer value
                var defaultValue = p.PropertyType.IsValueType ? Activator.CreateInstance(p.PropertyType) : null;
                members.Add(new(p.Get, p.ShouldSerialize, condition, defaultValue, For(p.PropertyType), set, parameter));
            }
            _members = [.. members];
        }

        public override object? Clone(object? value, int depth)
        {
            if (value is null) return null;
            if (_fallback is not null) return _fallback.Clone(value, depth);
            Deeper(depth);
            object result;
            if (_construct is null) result = _create!();
            else
            {
                var args = (object?[])_parameterDefaults.Clone(); // a parameter whose property is not written gets its default, as when the JSON lacks it
                foreach (var m in _members)
                    if (m.Parameter >= 0 && Written(m, value, out var v)) args[m.Parameter] = m.Value.Clone(v, depth + 1);
                result = _construct(args);
            }
            foreach (var m in _members)
                if (m.Parameter < 0 && Written(m, value, out var v)) m.Set!(result, m.Value.Clone(v, depth + 1));
            return result;
        }

        private static bool Written(Member m, object owner, out object? v)
        {
            v = m.Get(owner);
            if (m.ShouldSerialize is { } should && !should(owner, v)) return false;
            return m.Condition switch
            {
                JsonIgnoreCondition.WhenWritingNull => v is not null,
                JsonIgnoreCondition.WhenWritingDefault => v is not null && !v.Equals(m.DefaultValue),
                _ => true,
            };
        }
    }
}
