using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
#if NET9_0_OR_GREATER
using Lock = System.Threading.Lock;
#else
using Lock = object;
#endif

namespace VisualProfiler;

static class GeneralStringCache
{
    static readonly Dictionary<string, int> stringsToIds = [];
    static readonly Dictionary<int, string> idsToStrings = [];
    static readonly Lock lockObj = new();
    static int idGenerator = 1;

    public static void Init(Dictionary<int, string> values)
    {
        int max = 0;

        foreach (var (key, value) in values)
        {
            idsToStrings.Add(key, value);
            stringsToIds.Add(value, key);

            if (key > max)
                max = key;
        }

        idGenerator = max + 1;
    }

    public static StringId GetOrAdd(string? value)
    {
        if (value == null)
            return default;

        int id;

        lock (lockObj)
        {
            if (!stringsToIds.TryGetValue(value, out id))
            {
                id = idGenerator++;
                stringsToIds.Add(value, id);
                idsToStrings.Add(id, value);
            }
        }

        return new StringId(id);
    }

    public static bool TryGet(string value, out StringId id)
    {
        int index;

        lock (lockObj)
        {
            if (!stringsToIds.TryGetValue(value, out index))
            {
                id = default;
                return false;
            }
        }

        id = new StringId(index);
        return true;
    }

    public static string? Get(StringId id)
    {
        if (id.ID == 0)
            return null;

        lock (lockObj)
            return idsToStrings[id.ID];
    }

    public static string? Intern(string? value)
    {
        if (value == null)
            return null;

        lock (lockObj)
        {
            if (stringsToIds.TryGetValue(value, out int id))
                return idsToStrings[id];

            id = idGenerator++;
            stringsToIds.Add(value, id);
            idsToStrings.Add(id, value);
        }

        return value;
    }

    public static Dictionary<int, string> GetStrings() => new(idsToStrings);

    public static void Clear()
    {
        stringsToIds.Clear();
        idsToStrings.Clear();
        idGenerator = 1;
    }
}

[ProtoContract]
struct StringId
{
    [ProtoMember(1)] public int ID;

    [ProtoIgnore]
    public string? String
    {
        get
        {
            if (_string == null && ID > 0)
                _string = GeneralStringCache.Get(this);

            return _string;
        }
        set
        {
            _string = value;
            ID = GeneralStringCache.GetOrAdd(value).ID;
        }
    }
    string? _string;

    public StringId(int id)
    {
        ID = id;
    }

    public StringId(string? _string)
    {
        this._string = _string;
        ID = GeneralStringCache.GetOrAdd(_string).ID;
    }

    public override string? ToString() => String;

    public static implicit operator string?(StringId sid) => sid.String;
}
