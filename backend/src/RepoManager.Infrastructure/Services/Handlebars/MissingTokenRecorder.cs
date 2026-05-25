using HandlebarsDotNet.IO;

namespace RepoManager.Infrastructure.Services.Handlebars;

/// <summary>
/// Tracks unknown {{custom.*}} token accesses during Handlebars rendering.
/// The recording dictionary intercepts missing key lookups on the custom variable dict.
/// </summary>
public class MissingTokenRecorder : IFormatterProvider
{
    [ThreadStatic]
    private static HashSet<string>? _bag;

    public void BeginCapture() => _bag = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> EndCapture()
    {
        var result = (IReadOnlySet<string>?)_bag ?? new HashSet<string>();
        _bag = null;
        return result;
    }

    public IDictionary<string, string> CreateRecordingDictionary(
        IReadOnlyDictionary<string, string> source) =>
        new RecordingCustomDictionary(source, this);

    // IFormatterProvider — not used for recording but required by interface
    public bool TryCreateFormatter(Type type, out IFormatter? formatter)
    {
        formatter = null;
        return false;
    }

    private void RecordMissingToken(string key)
    {
        _bag?.Add($"custom.{key}");
    }

    private sealed class RecordingCustomDictionary : IDictionary<string, string>
    {
        private readonly IReadOnlyDictionary<string, string> _source;
        private readonly MissingTokenRecorder _recorder;

        public RecordingCustomDictionary(
            IReadOnlyDictionary<string, string> source,
            MissingTokenRecorder recorder)
        {
            _source = source;
            _recorder = recorder;
        }

        public string this[string key]
        {
            get
            {
                if (_source.TryGetValue(key, out var val)) return val;
                _recorder.RecordMissingToken(key);
                return string.Empty;
            }
            set => throw new NotSupportedException();
        }

        public bool TryGetValue(string key, out string value)
        {
            if (_source.TryGetValue(key, out value!)) return true;
            _recorder.RecordMissingToken(key);
            value = string.Empty;
            // Return true so Handlebars renders empty string rather than skipping the block
            return true;
        }

        // Always report key as present so Handlebars doesn't short-circuit to UndefinedBindingResult
        public bool ContainsKey(string key) => true;

        public ICollection<string> Keys => _source.Keys.ToList();
        public ICollection<string> Values => _source.Values.ToList();
        public int Count => _source.Count;
        public bool IsReadOnly => true;
        public void Add(string key, string value) => throw new NotSupportedException();
        public bool Remove(string key) => throw new NotSupportedException();
        public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(KeyValuePair<string, string> item) =>
            _source.TryGetValue(item.Key, out var v) && v == item.Value;
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, string>>)_source).CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _source.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
