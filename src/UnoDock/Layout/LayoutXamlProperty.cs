using System.Runtime.ExceptionServices;

namespace UnoDock.Layout;
/// <summary>
/// Native dependency-property endpoints for the existing transactional layout
/// model. Getters/setters and their validation remain the only model authority.
/// No reflected member access, parallel activation engine or event subscription.
/// </summary>
internal sealed class LayoutXamlProperty
{
    private static readonly Dictionary<Type, Dictionary<string, LayoutXamlProperty>> Registry = [];
    private static readonly object Gate = new();
    private readonly Func<LayoutElement, object?> _read;
    private readonly Action<LayoutElement, object?> _write;
    private DependencyProperty _property = null!;
    private LayoutXamlProperty(Func<LayoutElement, object?> read, Action<LayoutElement, object?> write)
    {
        _read = read;
        _write = write;
    }

    internal static DependencyProperty Register<TOwner, TValue>(string name, TValue defaultValue, Func<TOwner, TValue> read, Action<TOwner, TValue> write)
        where TOwner : LayoutElement
    {
        var endpoint = new LayoutXamlProperty(owner => read((TOwner)owner), (owner, value) => write((TOwner)owner, (TValue)value!));
        endpoint._property = DependencyProperty.Register(name, typeof(TValue), typeof(TOwner), new PropertyMetadata(defaultValue, (owner, args) => endpoint.Changed((LayoutElement)owner, args.NewValue)));
        lock (Gate)
        {
            if (!Registry.TryGetValue(typeof(TOwner), out var properties))
            {
                Registry.Add(typeof(TOwner), properties = new(StringComparer.Ordinal));
            }

            properties.Add(name, endpoint);
        }

        return endpoint._property;
    }

    internal static void Publish(LayoutElement owner, string name)
    {
        LayoutXamlProperty? endpoint = null;
        lock (Gate)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                if (Registry.TryGetValue(type, out var properties) && properties.TryGetValue(name, out endpoint))
                {
                    break;
                }
            }
        }

        endpoint?.Synchronize(owner);
    }

    private void Synchronize(LayoutElement owner)
    {
        var actual = _read(owner);
        if (!Equals(owner.GetValue(_property), actual))
        {
            owner.SetValue(_property, actual);
        }
    }

    private void Changed(LayoutElement owner, object? requested)
    {
        if (Equals(_read(owner), requested))
        {
            return; // Publication of the model's already committed value.
        }

        Exception? failure = null;
        try
        {
            _write(owner, requested);
        }
        catch (Exception error)
        {
            failure = error;
        }

        // Rejected/coerced requests must not leave a DP value which disagrees
        // with its CLR model. A callback's completed replacement is authoritative.
        try
        {
            Synchronize(owner);
        }
        catch (Exception error)
        {
            failure = failure == null ? error : new AggregateException(failure, error);
        }

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
