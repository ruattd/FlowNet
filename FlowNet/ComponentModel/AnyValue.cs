using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace FlowNet.ComponentModel;

/// <summary>
/// 任意类型值容器。<br/>
/// 对常用的值类型通过 C 风格 union 直接内联存储，引用类型存储其引用，其他自定义值类型装箱后以引用形式存储并在获取时解引用。
/// </summary>
public readonly struct AnyValue
{
    /// <summary>
    /// 当前存储值的种类标记。
    /// </summary>
    public enum Tag : byte
    {
        /// <summary>空的 <see cref="AnyValue"/>，常见于 CLR 默认值等初始化状态。</summary>
        Empty = 0,
        Bool, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64,
        Single, Double, Char, IntPtr, UIntPtr,
        DateTime, TimeSpan, DateTimeOffset, Guid, Decimal,
        Complex,
#if NET5_0_OR_GREATER
        Half, Rune, Index, Range,
        Vector2, Vector3, Vector4, Quaternion, Plane,
#endif
#if NET6_0_OR_GREATER
        DateOnly, TimeOnly,
#endif
#if NET7_0_OR_GREATER
        Int128, UInt128,
#endif
        /// <summary>引用类型或装箱后的非基本值类型。</summary>
        Reference,
    }

    /// <summary>
    /// 基本数据类型的 union 存储; 所有字段共享同一段 16 字节内存。<br/>
    /// 基本数值类型通过显式字段访问；其他被支持的内联值类型（<see cref="System.DateTime"/>、<see cref="System.Guid"/> 等）
    /// 通过 <see cref="Unsafe.As{TFrom,TTo}(ref TFrom)"/> 重解释内存布局。
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PrimitiveUnion
    {
        [FieldOffset(0)] public bool Bool;
        [FieldOffset(0)] public byte Byte;
        [FieldOffset(0)] public sbyte SByte;
        [FieldOffset(0)] public short Int16;
        [FieldOffset(0)] public ushort UInt16;
        [FieldOffset(0)] public int Int32;
        [FieldOffset(0)] public uint UInt32;
        [FieldOffset(0)] public long Int64;
        [FieldOffset(0)] public ulong UInt64;
        [FieldOffset(0)] public float Single;
        [FieldOffset(0)] public double Double;
        [FieldOffset(0)] public char Char;
        [FieldOffset(0)] public nint IntPtr;
        [FieldOffset(0)] public nuint UIntPtr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Tag TagOf<T>()
    {
        if (typeof(T) == typeof(bool)) return Tag.Bool;
        if (typeof(T) == typeof(byte)) return Tag.Byte;
        if (typeof(T) == typeof(sbyte)) return Tag.SByte;
        if (typeof(T) == typeof(short)) return Tag.Int16;
        if (typeof(T) == typeof(ushort)) return Tag.UInt16;
        if (typeof(T) == typeof(int)) return Tag.Int32;
        if (typeof(T) == typeof(uint)) return Tag.UInt32;
        if (typeof(T) == typeof(long)) return Tag.Int64;
        if (typeof(T) == typeof(ulong)) return Tag.UInt64;
        if (typeof(T) == typeof(float)) return Tag.Single;
        if (typeof(T) == typeof(double)) return Tag.Double;
        if (typeof(T) == typeof(char)) return Tag.Char;
        if (typeof(T) == typeof(nint)) return Tag.IntPtr;
        if (typeof(T) == typeof(nuint)) return Tag.UIntPtr;
        if (typeof(T) == typeof(DateTime)) return Tag.DateTime;
        if (typeof(T) == typeof(TimeSpan)) return Tag.TimeSpan;
        if (typeof(T) == typeof(DateTimeOffset)) return Tag.DateTimeOffset;
        if (typeof(T) == typeof(Guid)) return Tag.Guid;
        if (typeof(T) == typeof(decimal)) return Tag.Decimal;
        if (typeof(T) == typeof(Complex)) return Tag.Complex;
#if NET5_0_OR_GREATER
        if (typeof(T) == typeof(Half)) return Tag.Half;
        if (typeof(T) == typeof(Rune)) return Tag.Rune;
        if (typeof(T) == typeof(Index)) return Tag.Index;
        if (typeof(T) == typeof(Range)) return Tag.Range;
        if (typeof(T) == typeof(Vector2)) return Tag.Vector2;
        if (typeof(T) == typeof(Vector3)) return Tag.Vector3;
        if (typeof(T) == typeof(Vector4)) return Tag.Vector4;
        if (typeof(T) == typeof(Quaternion)) return Tag.Quaternion;
        if (typeof(T) == typeof(Plane)) return Tag.Plane;
#endif
#if NET6_0_OR_GREATER
        if (typeof(T) == typeof(DateOnly)) return Tag.DateOnly;
        if (typeof(T) == typeof(TimeOnly)) return Tag.TimeOnly;
#endif
#if NET7_0_OR_GREATER
        if (typeof(T) == typeof(Int128)) return Tag.Int128;
        if (typeof(T) == typeof(UInt128)) return Tag.UInt128;
#endif
        return Tag.Empty;
    }

    /// <summary>
    /// 将 <see cref="PrimitiveUnion"/> 内存重解释为 <typeparamref name="T"/> 的引用。返回 ref 既可用于读取也可用于赋值写入。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T As<T>(ref PrimitiveUnion u) => ref Unsafe.As<PrimitiveUnion, T>(ref u);

    /// <summary>
    /// 用指定的 <paramref name="value"/> 和 <paramref name="tag"/> 打包出一个内联存储的 <see cref="AnyValue"/>。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static AnyValue Pack<T>(T value, Tag tag)
    {
        var union = default(PrimitiveUnion);
        As<T>(ref union) = value;
        return new AnyValue(union, tag);
    }

    public static readonly AnyValue Null = new(null);

    private readonly PrimitiveUnion _primitive;
    private readonly object? _reference;

    /// <summary>
    /// 当前存储值的种类标记。
    /// </summary>
    public Tag StoredTag { get; }

    /// <summary>
    /// 获取未知类型的值，对基本数据类型可能产生装箱和新对象分配。
    /// </summary>
    public object? ValueNoType
    {
        get
        {
            var u = _primitive;
            return StoredTag switch
            {
                Tag.Empty => null,
                Tag.Bool => _primitive.Bool,
                Tag.Byte => _primitive.Byte,
                Tag.SByte => _primitive.SByte,
                Tag.Int16 => _primitive.Int16,
                Tag.UInt16 => _primitive.UInt16,
                Tag.Int32 => _primitive.Int32,
                Tag.UInt32 => _primitive.UInt32,
                Tag.Int64 => _primitive.Int64,
                Tag.UInt64 => _primitive.UInt64,
                Tag.Single => _primitive.Single,
                Tag.Double => _primitive.Double,
                Tag.Char => _primitive.Char,
                Tag.IntPtr => _primitive.IntPtr,
                Tag.UIntPtr => _primitive.UIntPtr,
                Tag.DateTime => As<DateTime>(ref u),
                Tag.TimeSpan => As<TimeSpan>(ref u),
                Tag.DateTimeOffset => As<DateTimeOffset>(ref u),
                Tag.Guid => As<Guid>(ref u),
                Tag.Decimal => As<decimal>(ref u),
                Tag.Complex => As<Complex>(ref u),
#if NET5_0_OR_GREATER
                Tag.Half => As<Half>(ref u),
                Tag.Rune => As<Rune>(ref u),
                Tag.Index => As<Index>(ref u),
                Tag.Range => As<Range>(ref u),
                Tag.Vector2 => As<Vector2>(ref u),
                Tag.Vector3 => As<Vector3>(ref u),
                Tag.Vector4 => As<Vector4>(ref u),
                Tag.Quaternion => As<Quaternion>(ref u),
                Tag.Plane => As<Plane>(ref u),
#endif
#if NET6_0_OR_GREATER
                Tag.DateOnly => As<DateOnly>(ref u),
                Tag.TimeOnly => As<TimeOnly>(ref u),
#endif
#if NET7_0_OR_GREATER
                Tag.Int128 => As<Int128>(ref u),
                Tag.UInt128 => As<UInt128>(ref u),
#endif
                Tag.Reference => _reference,
                _ => null,
            };
        }
    }

    /// <summary>
    /// 是否为空。多用于判断当前实例是否为 <see cref="AnyValue"/> 的 CLR 默认值。
    /// </summary>
    public bool IsEmpty => StoredTag == Tag.Empty;

    private AnyValue(PrimitiveUnion primitive, Tag tag)
    {
        _primitive = primitive;
        _reference = null;
        StoredTag = tag;
    }

    private AnyValue(object? value)
    {
        _primitive = default;
        _reference = value;
        StoredTag = Tag.Reference;
    }

    /// <summary>
    /// 创建 <see cref="AnyValue"/>。对基本数据类型走特化路径无装箱；其他值类型会被装箱后存储引用。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyValue Of<T>(T value)
    {
        var tag = TagOf<T>();
        return tag == Tag.Empty ? new AnyValue(value) : Pack(value, tag);
    }

    /// <summary>
    /// 创建 <see cref="AnyValue"/>，自动探测传入对象的运行时类型并使用对应类型的特化路径。<br/>
    /// 对基本数据类型会从装箱对象中解出原始值后内联存储；其他对象按引用存储。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AnyValue OfObject(object? value) => value switch
    {
        null => Null,
        bool v => Pack(v, Tag.Bool),
        byte v => Pack(v, Tag.Byte),
        sbyte v => Pack(v, Tag.SByte),
        short v => Pack(v, Tag.Int16),
        ushort v => Pack(v, Tag.UInt16),
        int v => Pack(v, Tag.Int32),
        uint v => Pack(v, Tag.UInt32),
        long v => Pack(v, Tag.Int64),
        ulong v => Pack(v, Tag.UInt64),
        float v => Pack(v, Tag.Single),
        double v => Pack(v, Tag.Double),
        char v => Pack(v, Tag.Char),
        nint v => Pack(v, Tag.IntPtr),
        nuint v => Pack(v, Tag.UIntPtr),
        DateTime v => Pack(v, Tag.DateTime),
        TimeSpan v => Pack(v, Tag.TimeSpan),
        DateTimeOffset v => Pack(v, Tag.DateTimeOffset),
        Guid v => Pack(v, Tag.Guid),
        decimal v => Pack(v, Tag.Decimal),
        Complex v => Pack(v, Tag.Complex),
#if NET5_0_OR_GREATER
        Half v => Pack(v, Tag.Half),
        Rune v => Pack(v, Tag.Rune),
        Index v => Pack(v, Tag.Index),
        Range v => Pack(v, Tag.Range),
        Vector2 v => Pack(v, Tag.Vector2),
        Vector3 v => Pack(v, Tag.Vector3),
        Vector4 v => Pack(v, Tag.Vector4),
        Quaternion v => Pack(v, Tag.Quaternion),
        Plane v => Pack(v, Tag.Plane),
#endif
#if NET6_0_OR_GREATER
        DateOnly v => Pack(v, Tag.DateOnly),
        TimeOnly v => Pack(v, Tag.TimeOnly),
#endif
#if NET7_0_OR_GREATER
        Int128 v => Pack(v, Tag.Int128),
        UInt128 v => Pack(v, Tag.UInt128),
#endif
        _ => new AnyValue(value),
    };

    /// <summary>
    /// 获取指定类型的值。
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <returns>值</returns>
    /// <exception cref="InvalidCastException">指定类型与真实类型不匹配</exception>
    public T Get<T>() => TryGet<T>(out var v) ? v
        : throw new InvalidCastException($"Type mismatch: stored tag {StoredTag}, requested {typeof(T).FullName}");

    /// <summary>
    /// 尝试获取指定类型的值。
    /// </summary>
    /// <param name="result">值，若指定类型与真实类型不匹配则为该类型默认值</param>
    /// <typeparam name="T">值的类型</typeparam>
    /// <returns>类型是否匹配，若不匹配则返回 <see langword="false"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>(out T result)
    {
        var expected = TagOf<T>();
        if (expected == Tag.Empty)
        {
            if (_reference is T t)
            {
                result = t;
                return true;
            }
        }
        else if (expected == StoredTag)
        {
            var u = _primitive;
            result = As<T>(ref u);
            return true;
        }
        result = default!;
        return false;
    }
}
