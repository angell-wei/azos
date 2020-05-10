/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

using Azos.Conf;
using Azos.Data;

namespace Azos.Serialization.Bix
{
  /// <summary>
  /// Generates code for serializer and deserializer
  /// </summary>
  public partial class CodeGenerator : DisposableObject
  {

    [Config]
    public string RootPath{ get; set;}

    [Config]
    public GeneratedCodeOrganization CodeOrganization{ get; set;}

    [Config(Default = 255)]
    public int HeaderDetailLevel { get; set; } = 255;

    public void Generate(Assembly asm)
    {
      asm.NonNull(nameof(asm));
      if (!Directory.Exists(RootPath)) throw new BixException(StringConsts.BIX_GENERATOR_PATH_DOESNOT_EXIST_ERROR.Args(RootPath));
      DoGenerate(asm);
    }

    protected virtual void DoGenerate(Assembly asm)
    {
      var types = GetBixTypes(asm);
      string ns = null;
      string tn = null;
      StringBuilder source = null;
      foreach(var tdoc in types)
      {
        if (source==null ||
            CodeOrganization == GeneratedCodeOrganization.FilePerType ||
            (CodeOrganization == GeneratedCodeOrganization.FilePerNamespace && tdoc.Namespace!=ns))
        {
          EmitFileFooter(source);
          WriteContent(ns, tn, source);
          source = new StringBuilder();
          EmitFileHeader(source);
          ns = null;
        }

        if (tdoc.Namespace!=ns)
        {
          if (ns.IsNotNullOrWhiteSpace()) EmitNamespaceFooter(source);
          EmitNamespaceHeader(source, tdoc.Namespace);
        }

        ns = tdoc.Namespace;
        tn = tdoc.Name;
        EmitBixCores(source, tdoc);
      }
      EmitNamespaceFooter(source);
      EmitFileFooter(source);
      WriteContent(ns, tn, source);
    }

    protected virtual void WriteContent(string ns, string name, StringBuilder content)
    {
      if (content==null || content.Length==0 || name.IsNullOrWhiteSpace()) return;

      if (ns!=null)
        ns = ns.Replace('.', Path.DirectorySeparatorChar);

      if (CodeOrganization != GeneratedCodeOrganization.FilePerType) name = "_bixgen";

      var path = Path.Combine(this.RootPath, ns);
      IOUtils.EnsureAccessibleDirectory(path);

      var fn = Path.Combine(path, name+".cs");

      File.WriteAllText(fn, content.ToString());
    }

    public static IEnumerable<Type> DefaultScanForAllBixTypes(Assembly asm)
    {
      var allTypes = asm.NonNull(nameof(asm)).GetTypes();

      var result = allTypes.Where(t => t.IsClass &&
                                       !t.IsAbstract &&
                                       !t.IsGenericTypeDefinition &&
                                       Attribute.IsDefined(t, typeof(BixAttribute), false) && typeof(TypedDoc).IsAssignableFrom(t))
                                .OrderBy(t => t.Namespace)
                                .ToArray();
      return result;
    }


    protected virtual IEnumerable<Type> GetBixTypes(Assembly asm)
    {
      var types = DefaultScanForAllBixTypes(asm);
      return types;
    }

    /// <summary>
    /// Converts backend name to ulong using Atom
    /// </summary>
    public static ulong GetName(string name) => Atom.Encode(name).ID;

    /// <summary>
    /// Converts backend name from ulong Atom value to string. Atom performs result interning
    /// </summary>
    public static unsafe string GetName(ulong name) => new Atom(name).Value;


    protected virtual void EmitFileHeader(StringBuilder source)
    {
      var bi = BuildInformation.ForFramework;

      if (HeaderDetailLevel>0)
      {
        source.AppendLine("// Do not modify by hand. This file is auto-generated by Bix code generator");

        if (HeaderDetailLevel > 2)
          source.AppendLine("// Generated on {0} by {1} at {2}".Args(DateTime.Now, Environment.UserName, Environment.MachineName));

        if (HeaderDetailLevel > 1)
          source.AppendLine("// Framework: " + bi.ToString());

        source.AppendLine();
      }

      source.AppendLine("using System;");
      source.AppendLine("using System.Collections.Generic;");
      source.AppendLine();
      source.AppendLine("using Azos;");
      source.AppendLine("using Azos.IO;");
      source.AppendLine("using Azos.Data;");
      source.AppendLine("using Azos.Serialization.Bix;");
      source.AppendLine();
      source.AppendLine("using BWR = Azos.Serialization.Bix.Writer;");
      source.AppendLine("using BRD = Azos.Serialization.Bix.Reader;");
      source.AppendLine();
    }

    protected virtual void EmitNamespaceHeader(StringBuilder source, string ns)
    {
      if (source==null) return;
      source.AppendLine("namespace {0}._bix_generated".Args(ns));
      source.AppendLine("{");
    }

    protected virtual void EmitNamespaceFooter(StringBuilder source)
    {
      if (source==null) return;
      source.AppendLine("}//namespace");
      source.AppendLine();
    }

    protected virtual void EmitFileFooter(StringBuilder source)
    {
      if (source==null) return;
      source.AppendLine();
      source.AppendLine("//EOF");
    }

    public static IEnumerable<string> DefaultScanForAllBixTargetNames(Schema schema)
     =>  schema.SelectMany(fd => fd.Attrs)
               .Where(atr => atr.IsArow)//todo isBix
               .Select(atr => atr.TargetName)
               .Distinct(StringComparer.InvariantCultureIgnoreCase);

    protected virtual void EmitBixCores(StringBuilder source, Type tDoc)
    {
      if (source==null) return;
      var schema = Schema.GetForTypedDoc(tDoc);
      var allTargets = DefaultScanForAllBixTargetNames(schema);

      foreach(var target in allTargets)
      {
        EmitBixCoreForTarget(source, tDoc, target.IsNotNullOrWhiteSpace() ? target : TargetedAttribute.ANY_TARGET);
      }
    }

    public static string GetClassName(Type tDoc, string targetName)
    {
      var trg = targetName;
      if (trg==TargetedAttribute.ANY_TARGET) trg = "ANY";
      else if (trg=="ANY") trg = targetName.ToMD5String();
      else if (trg.Length > 16 || !CodeAnalysis.CSharp.CSIdentifiers.Validate(targetName))
       trg = targetName.ToMD5String();

      return "{0}_{1}_BixCore".Args(tDoc.Name, trg);
    }

    protected virtual void EmitBixCoreForTarget(StringBuilder source, Type tDoc, string targetName)
    {
      var cname = GetClassName(tDoc, targetName);

      source.AppendLine("  ///<summary>");
      source.AppendLine("  /// Generated BixCore implementation for data document:");
      source.AppendLine("  ///   \"{0}\" targeting \"{1}\"".Args(tDoc.FullName, targetName));
      source.AppendLine("  ///</summary>");
      source.AppendLine("  internal class {0} : BixCore<{1}>".Args(cname, tDoc.FullNameWithExpandedGenericArgs(verbatimPrefix: true)));
      source.AppendLine("  {");
      var schema = Schema.GetForTypedDoc(tDoc);

      source.AppendLine("    public {0}() : base({1}) {{ }}".Args(cname, targetName));
      source.AppendLine();

      EmitSerialize(source, schema, targetName);
      source.AppendLine();

      EmitDeserialize(source, schema, targetName);

      source.AppendLine("  }//class " + cname);
      source.AppendLine();
    }

  }
}