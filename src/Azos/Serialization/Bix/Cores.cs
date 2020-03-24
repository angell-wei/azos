﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using Azos.Data;

namespace Azos.Serialization.Bix
{
  /// <summary>
  /// Designates classes which implement Serialization and Deserialization core for specific types
  /// These classes are generated by CLI Bix compiler
  /// </summary>
  public abstract class BixCore
  {
    /// <summary> Returns the target type which this BixCore implementation handles </summary>
    public abstract TargetedType TargetedType{ get; }

    /// <summary>
    /// Serializes typed data document into BixWriter
    /// </summary>
    public abstract void Serialize(BixWriter writer, TypedDoc doc, BixContext ctx);

    /// <summary>
    /// Deserializes typed data document from BixReader
    /// </summary>
    public abstract void Deserialize(BixReader reader, TypedDoc doc, BixContext ctx);
  }

  /// <summary>
  /// Designates classes which implement Serialization and Deserialization core for specific types
  /// Concrete generated class implementations derive from this class
  /// </summary>
  public abstract class BixCore<T> : BixCore where T : TypedDoc
  {
    protected BixCore(string targetName)
    {
      m_TargetedType = new TargetedType(targetName, typeof(T));
    }

    private TargetedType m_TargetedType;

    public sealed override TargetedType TargetedType => m_TargetedType;

    /// <summary>
    /// Serializes typed data document into BixWriter
    /// </summary>
    public sealed override void Serialize(BixWriter writer, TypedDoc doc, BixContext ctx) => SerializeCore(writer, (T)doc, ctx);

    /// <summary>
    /// Serializes typed data document into BixWriter
    /// </summary>
    public sealed override void Deserialize(BixReader reader, TypedDoc doc, BixContext ctx) => DeserializeCore(reader, (T)doc, ctx);

    /// <summary>
    /// Serializes typed data document into BixWriter
    /// </summary>
    public abstract void SerializeCore(BixWriter writer, T doc, BixContext ctx);

    /// <summary>
    /// Deserializes typed data document from BixWriter
    /// </summary>
    public abstract void DeserializeCore(BixReader reader, T doc, BixContext ctx);
  }
}
