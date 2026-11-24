using System;

namespace TownOfHost;

/// <summary>
/// ゲーム側のリストのコピーを、同じフレームの間だけ使い回すための入れ物。
/// フレーム・リスト本体・人数・リストの変更回数(_version)がすべて同じ時だけ前のコピーを返す。
/// 変更回数は追加・削除・途中の入れ替え(インデクサへの代入)・Clear のたびに増えるので、
/// 人数や最後の要素が同じまま途中だけ入れ替わった場合でも、古い一覧は返さない。
/// </summary>
internal sealed class FrameSnapshotCache<T> where T : class
{
    private T[] items = Array.Empty<T>();
    private bool has;
    private int frame, count, version;
    private IntPtr source;

    public T[] Get<TState>(int frame, IntPtr source, int count, int version, TState state, Func<TState, int, T> at)
    {
        if (has && frame == this.frame && source == this.source && count == this.count && version == this.version)
            return items;
        var copy = new T[count];
        for (int i = 0; i < count; i++) copy[i] = at(state, i);
        items = copy;
        this.frame = frame; this.source = source; this.count = count; this.version = version;
        has = true;
        return items;
    }

    /// <summary>メインスレッド以外など、使い回せない時の毎回のコピー(従来どおり)</summary>
    public static T[] Copy<TState>(int count, TState state, Func<TState, int, T> at)
    {
        var copy = new T[count];
        for (int i = 0; i < count; i++) copy[i] = at(state, i);
        return copy;
    }
}
