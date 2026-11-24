// PlayerCatch の一覧キャッシュ(FrameSnapshotCache)の回帰テスト。
// FakeList は Il2Cpp(Mono) の List<T> と同じく、追加・削除・インデクサ代入・Clear のたびに _version を増やす。
using TownOfHost;

sealed class P { public readonly int Id; public P(int id) { Id = id; } public override string ToString() => "P" + Id; }

sealed class FakeList
{
    readonly List<P> items = new();
    public int Version { get; private set; }
    public int Count => items.Count;
    public P this[int i] { get => items[i]; set { items[i] = value; Version++; } }
    public void Add(P p) { items.Add(p); Version++; }
    public void RemoveAt(int i) { items.RemoveAt(i); Version++; }
    public void Insert(int i, P p) { items.Insert(i, p); Version++; }
    public void Clear() { items.Clear(); Version++; }
    public P[] ToArray() => items.ToArray();
}

static class Program
{
    static int passed, failed;
    static readonly Func<FakeList, int, P> At = (l, i) => l[i];

    static void Test(string name, Action body)
    {
        try { body(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Same(P[] expected, P[] actual, string message)
    {
        Check(expected.Length == actual.Length && expected.Zip(actual).All(x => ReferenceEquals(x.First, x.Second)),
            $"{message}: expected [{string.Join(",", (object[])expected)}] actual [{string.Join(",", (object[])actual)}]");
    }

    static readonly IntPtr Source = new(0x1000);
    static P[] Get(FrameSnapshotCache<P> cache, FakeList list, int frame, IntPtr? source = null)
        => cache.Get(frame, source ?? Source, list.Count, list.Version, list, At);

    static FakeList Players(int n)
    {
        var list = new FakeList();
        for (int i = 0; i < n; i++) list.Add(new P(i));
        return list;
    }

    static int Main()
    {
        Test("same frame without change reuses the copy", () =>
        {
            var list = Players(5); var cache = new FrameSnapshotCache<P>();
            var a = Get(cache, list, 1); var b = Get(cache, list, 1);
            Check(ReferenceEquals(a, b), "copy was not reused");
            Same(list.ToArray(), b, "content");
        });

        Test("middle player replaced in same frame (count and last unchanged) returns new list", () =>
        {
            var list = Players(5); var cache = new FrameSnapshotCache<P>();
            var before = Get(cache, list, 7);
            var last = list[4];
            var newcomer = new P(99);
            list[2] = newcomer; // 途中だけ入れ替え。人数・最後の要素は同じ
            Check(list.Count == 5 && ReferenceEquals(list[4], last), "precondition");
            var after = Get(cache, list, 7);
            Check(!ReferenceEquals(before, after), "stale copy returned");
            Check(after.Contains(newcomer) && !after.Any(p => p.Id == 2), "replaced player not reflected");
            Same(list.ToArray(), after, "content");
        });

        Test("middle removed then re-inserted elsewhere in same frame returns new list", () =>
        {
            var list = Players(6); var cache = new FrameSnapshotCache<P>();
            Get(cache, list, 3);
            var moved = list[1]; list.RemoveAt(1); list.Insert(3, moved);
            Same(list.ToArray(), Get(cache, list, 3), "content");
        });

        Test("remove then add in same frame returns new list", () =>
        {
            var list = Players(4); var cache = new FrameSnapshotCache<P>();
            Get(cache, list, 2);
            list.RemoveAt(0); list.Add(new P(50));
            Same(list.ToArray(), Get(cache, list, 2), "content");
        });

        Test("clear and refill to same count in same frame returns new list", () =>
        {
            var list = Players(3); var cache = new FrameSnapshotCache<P>();
            Get(cache, list, 9);
            list.Clear(); for (int i = 10; i < 13; i++) list.Add(new P(i));
            Same(list.ToArray(), Get(cache, list, 9), "content");
        });

        Test("next frame always takes a fresh copy", () =>
        {
            var list = Players(3); var cache = new FrameSnapshotCache<P>();
            var a = Get(cache, list, 1); var b = Get(cache, list, 2);
            Check(!ReferenceEquals(a, b), "copy reused across frames");
            Same(list.ToArray(), b, "content");
        });

        Test("different list object takes a fresh copy", () =>
        {
            var list = Players(3); var other = Players(3); var cache = new FrameSnapshotCache<P>();
            Get(cache, list, 1);
            Same(other.ToArray(), Get(cache, other, 1, new IntPtr(0x2000)), "content");
        });

        Test("empty list", () =>
        {
            var list = new FakeList(); var cache = new FrameSnapshotCache<P>();
            Check(Get(cache, list, 1).Length == 0, "not empty");
            list.Add(new P(1));
            Same(list.ToArray(), Get(cache, list, 1), "content after add");
        });

        Test("uncached copy matches the list", () =>
        {
            var list = Players(4);
            var a = FrameSnapshotCache<P>.Copy(list.Count, list, At); var b = FrameSnapshotCache<P>.Copy(list.Count, list, At);
            Check(!ReferenceEquals(a, b), "Copy must not share");
            Same(list.ToArray(), a, "content");
        });

        // 従来の挙動(呼ぶたびに ToArray)と、ランダムな操作の後で常に同じ内容になること
        Test("randomized operations always match copy-every-call", () =>
        {
            var rng = new Random(20261124);
            var list = Players(8); var cache = new FrameSnapshotCache<P>();
            int frame = 0, next = 100;
            for (int step = 0; step < 20000; step++)
            {
                switch (rng.Next(8))
                {
                    case 0: frame++; break;
                    case 1: if (list.Count < 20) list.Add(new P(next++)); break;
                    case 2: if (list.Count > 0) list.RemoveAt(rng.Next(list.Count)); break;
                    case 3: if (list.Count > 0) list[rng.Next(list.Count)] = new P(next++); break;
                    case 4: if (list.Count > 1) { int i = rng.Next(list.Count - 1); var p = list[i]; list.RemoveAt(i); list.Insert(i, new P(next++)); } break;
                    default: break; // 何も変えずに読むだけ(使い回しの経路)
                }
                Same(list.ToArray(), Get(cache, list, frame), $"step {step}");
            }
        });

        Console.WriteLine($"PLAYERCATCH_SNAPSHOT_RESULT {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
