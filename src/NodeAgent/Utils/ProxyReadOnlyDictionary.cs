using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NodeAgent.Utils;

public class ProxyReadOnlyDictionary<TKey, TReadOnlyValue, TValue> : IReadOnlyDictionary<TKey, TReadOnlyValue> 
    where TValue : TReadOnlyValue
{
    private readonly IDictionary<TKey, TValue> _dict;

    public ProxyReadOnlyDictionary(IDictionary<TKey, TValue> dict)
    {
        _dict = dict;
    }

    public TReadOnlyValue this[TKey key] => _dict[key];

    public IEnumerable<TKey> Keys => _dict.Keys;

    public IEnumerable<TReadOnlyValue> Values => (IEnumerable<TReadOnlyValue>)_dict.Values;

    public int Count => _dict.Count;

    public bool ContainsKey(TKey key)
    {
        return _dict.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TReadOnlyValue value)
    {
        var result = _dict.TryGetValue(key, out var value2);
        value = value2;
        return result;
    }

    public IEnumerator<KeyValuePair<TKey, TReadOnlyValue>> GetEnumerator()
    {
        foreach (var kvp in _dict)
        {
            yield return new KeyValuePair<TKey, TReadOnlyValue>(kvp.Key, kvp.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
