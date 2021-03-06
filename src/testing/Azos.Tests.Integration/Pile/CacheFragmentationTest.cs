/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Azos.Scripting;

using Azos.Apps;
using Azos.Pile;
using Azos.Data;

namespace Azos.Tests.Integration.Pile
{
  [Runnable]
  class CacheFragmentationTest : IRunHook
  {
    bool IRunHook.Prologue(Runner runner, FID id, MethodInfo method, RunAttribute attr, ref object[] args)
    {
      GC.Collect();
      return false;
    }

    bool IRunHook.Epilogue(Runner runner, FID id, MethodInfo method, RunAttribute attr, Exception error)
    {
      GC.Collect();
      return false;
    }


    [Run("speed=true   durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  deleteFreq=3  isParallel=true")]
    [Run("speed=false  durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  deleteFreq=3  isParallel=true")]
    public void DeleteOne_ByteArray(bool speed, int durationSec, int payloadSizeMin, int payloadSizeMax, int deleteFreq, bool isParallel)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (isParallel ? (System.Environment.ProcessorCount - 1) : 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var i = 0;
              var list = new List<CheckByteArray>();
              var tA = cache.GetOrCreateTable<GDID>("A");
              var wlc = 0;
              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) break;

                var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                var val = new byte[payloadSize];
                val[0] = (byte)Ambient.Random.NextRandomInteger;
                val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;

                var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, (ulong)i);
                tA.Put(key, val);

                list.Add(new CheckByteArray(key, payloadSize - 1, val[0], val[payloadSize - 1]));

                // delete ONE random element
                if (i > 0 && i % deleteFreq == 0)
                {
                  while (true && list.Count > 0)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    key = list[idx].Key;
                    var removed = tA.Remove(key);
                    list.RemoveAt(idx);
                    if (removed)
                      break;
                  }
                }

                // get several random elements
                if (list.Count > 64 && Ambient.Random.NextScaledRandomInteger(0, 100) > 98)
                {
                  var toRead = Ambient.Random.NextScaledRandomInteger(8, 64);
                  wlc++;
                  if (wlc % 125 == 0)
                    Console.WriteLine("Thread {0} is reading {1} elements, total {2}"
                      .Args(Thread.CurrentThread.ManagedThreadId, toRead, list.Count));
                  for (var k = 0; k < toRead && list.Count > 0; k++)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];
                    var buf = tA.Get(element.Key) as byte[];
                    if (buf == null)
                    {
                      list.RemoveAt(idx);
                      continue;
                    }
                    Aver.AreEqual(element.FirstByte, buf[0]);
                    Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
                  }

                }

                if (i == Int32.MaxValue)
                  i = 0;
                else
                  i++;

                if (list.Count == Int32.MaxValue)
                  list = new List<CheckByteArray>();
              }

              Console.WriteLine("Thread {0} is doing final read of {1} elements, tableCount {2}"
                .Args(Thread.CurrentThread.ManagedThreadId, list.Count, tA.Count));
              foreach (var element in list)
              {
                var buf = tA.Get(element.Key) as byte[];
                if (buf == null)
                  continue;
                Aver.AreEqual(element.FirstByte, buf[0]);
                Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
              }
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    [Run("speed=true   durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    [Run("speed=false  durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    public void Chessboard_ByteArray(bool speed, int durationSec, int payloadSizeMin, int payloadSizeMax, bool isParallel)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (isParallel ? (System.Environment.ProcessorCount - 1) : 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var list = new List<CheckByteArray>();
              var i = 0;
              var tA = cache.GetOrCreateTable<GDID>("A");
              var wlc = 0;
              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) break;

                var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                var val = new byte[payloadSize];
                val[0] = (byte)Ambient.Random.NextRandomInteger;
                val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;

                var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, (ulong)i);
                tA.Put(key, val);

                var element = new CheckByteArray(key, payloadSize - 1, val[0], val[payloadSize - 1]);
                list.Add(element);

                // delete previous element
                if (list.Count > 1 && i % 2 == 0)
                {
                  key = list[list.Count - 2].Key;
                  tA.Remove(key);
                  list.RemoveAt(list.Count - 2);
                }

                // get several random elements
                if (list.Count > 64 && Ambient.Random.NextScaledRandomInteger(0, 100) > 98)
                {
                  var toRead = Ambient.Random.NextScaledRandomInteger(8, 64);
                  wlc++;
                  if (wlc % 125 == 0)
                    Console.WriteLine("Thread {0} is reading {1} elements, total {2}"
                      .Args(Thread.CurrentThread.ManagedThreadId, toRead, list.Count));
                  for (var k = 0; k < toRead && list.Count > 0; k++)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    element = list[idx];
                    var buf = tA.Get(element.Key) as byte[];
                    if (buf == null)
                    {
                      list.RemoveAt(idx);
                      continue;
                    }
                    Aver.AreEqual(element.FirstByte, buf[0]);
                    Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
                  }
                }

                if (i == Int32.MaxValue)
                  i = 0;
                else
                  i++;

                if (list.Count == Int32.MaxValue)
                  list = new List<CheckByteArray>();
              }

              // total check
              Console.WriteLine("Thread {0} is doing final read of {1} elements, tableCount {2}"
                .Args(Thread.CurrentThread.ManagedThreadId, list.Count, tA.Count));
              foreach (var element in list)
              {
                var buf = tA.Get(element.Key) as byte[];
                if (buf == null)
                  continue;
                Aver.AreEqual(element.FirstByte, buf[0]);
                Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
              }
              return;
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    [Run("speed=true   durationSec=30  putMin=100  putMax=200  delFactor=4  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    [Run("speed=false  durationSec=30  putMin=100  putMax=200  delFactor=4  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    public void DeleteSeveral_ByteArray(bool speed, int durationSec, int putMin, int putMax, int delFactor, int payloadSizeMin, int payloadSizeMax, bool isParallel)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (isParallel ? (System.Environment.ProcessorCount - 1) : 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var list = new List<CheckByteArray>();
              var tA = cache.GetOrCreateTable<GDID>("A");
              ulong k = 0;
              var wlc = 0;

              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) break;

                var putCount = Ambient.Random.NextScaledRandomInteger(putMin, putMax);
                for (int i = 0; i < putCount; i++)
                {
                  var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                  var val = new byte[payloadSize];
                  val[0] = (byte)Ambient.Random.NextRandomInteger;
                  val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;
                  var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, k);

                  tA.Put(key, val);

                  list.Add(new CheckByteArray(key, payloadSize - 1, val[0], val[payloadSize - 1]));
                  k++;
                }

                int delCount = putCount / delFactor;
                for (int i = 0; i < delCount; i++)
                {
                  while (true && list.Count > 0)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var key = list[idx].Key;
                    var removed = tA.Remove(key);
                    list.RemoveAt(idx);
                    if (removed)
                      break;
                  }
                }

                // get several random elements
                if (list.Count > 64 && Ambient.Random.NextScaledRandomInteger(0, 100) > 98)
                {
                  var toRead = Ambient.Random.NextScaledRandomInteger(8, 64);
                  wlc++;
                  if (wlc % 125 == 0)
                    Console.WriteLine("Thread {0} is reading {1} elements, total {2}"
                      .Args(Thread.CurrentThread.ManagedThreadId, toRead, list.Count));
                  for (var j = 0; j < toRead && list.Count > 0; j++)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];
                    var buf = tA.Get(element.Key) as byte[];
                    if (buf == null)
                    {
                      list.RemoveAt(idx);
                      continue;
                    }
                    Aver.AreEqual(element.FirstByte, buf[0]);
                    Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
                  }
                }

                if (list.Count == Int32.MaxValue)
                  list = new List<CheckByteArray>();
              }

              // total check
              Console.WriteLine("Thread {0} is doing final read of {1} elements, tableCount {2}"
                .Args(Thread.CurrentThread.ManagedThreadId, list.Count, tA.Count));
              foreach (var element in list)
              {
                var val = tA.Get(element.Key) as byte[];
                if (val == null)
                  continue;
                Aver.AreEqual(element.FirstByte, val[0]);
                Aver.AreEqual(element.LastByte, val[element.IdxLast]);
              }
              return;
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    [Run("speed=true   durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  countMin=100000  countMax=200000")]
    [Run("speed=false  durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  countMin=100000  countMax=200000")]
    public void NoGrowth_ByteArray(bool speed, int durationSec, int payloadSizeMin, int payloadSizeMax, int countMin, int countMax)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (System.Environment.ProcessorCount - 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var tA = cache.GetOrCreateTable<GDID>("A");
              var list = new List<CheckByteArray>();
              bool put = true;

              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) return;

                if (put)
                {
                  var cnt = Ambient.Random.NextScaledRandomInteger(countMin, countMax);
                  for (int i = 0; i < cnt; i++)
                  {
                    var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                    var val = new byte[payloadSize];
                    val[0] = (byte)Ambient.Random.NextRandomInteger;
                    val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;
                    var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, (ulong)i);

                    tA.Put(key, val);

                    var element = new CheckByteArray(key, payloadSize - 1, val[0], val[payloadSize - 1]);
                    list.Add(element);
                  }
                  Console.WriteLine("Thread {0} put {1} objects".Args(Thread.CurrentThread.ManagedThreadId, list.Count));
                  put = false;
                }
                else
                {
                  var i = 0;
                  for (var j = 0; j < list.Count; j++)
                  {
                    var element = list[j];
                    var buf = tA.Get(element.Key) as byte[];
                    if (buf != null)
                    {
                      Aver.AreEqual(element.FirstByte, buf[0]);
                      Aver.AreEqual(element.LastByte, buf[element.IdxLast]);
                      tA.Remove(element.Key);
                      i++;
                    }
                  }
                  Console.WriteLine("Thread {0} deleted {1} objects".Args(Thread.CurrentThread.ManagedThreadId, i));
                  list.Clear();
                  put = true;
                }
              }
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    [Run("speed=true   durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  deleteFreq=3  isParallel=true")]
    [Run("speed=false  durationSec=30  payloadSizeMin=2  payloadSizeMax=1000  deleteFreq=3  isParallel=true")]
    public void DeleteOne_TwoTables_ByteArray(bool speed, int durationSec, int payloadSizeMin, int payloadSizeMax, int deleteFreq, bool isParallel)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (isParallel ? (System.Environment.ProcessorCount - 1) : 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var i = 0;
              var list = new List<Tuple<int, GDID, int, byte, byte>>();
              var tA = cache.GetOrCreateTable<GDID>("A");
              var tB = cache.GetOrCreateTable<GDID>("B");
              var wlc = 0;

              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) break;

                var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                var val = new byte[payloadSize];
                val[0] = (byte)Ambient.Random.NextRandomInteger;
                val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;

                var tableId = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                var table = tableId == 0 ? tA : tB;
                var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, (ulong)i);

                table.Put(key, val);

                list.Add(new Tuple<int, GDID, int, byte, byte>(tableId, key, payloadSize - 1, val[0], val[payloadSize - 1]));

                // delete ONE random element
                if (i > 0 && i % deleteFreq == 0)
                {
                  while (true && list.Count > 0)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];

                    table = element.Item1 == 0 ? tA : tB;
                    key = element.Item2;

                    var removed = table.Remove(key);
                    list.RemoveAt(idx);

                    if (removed)
                      break;
                  }
                }

                // get several random elements
                if (list.Count > 64 && Ambient.Random.NextScaledRandomInteger(0, 100) > 98)
                {
                  var toRead = Ambient.Random.NextScaledRandomInteger(8, 64);
                  wlc++;
                  if (wlc % 125 == 0)
                    Console.WriteLine("Thread {0} is reading {1} elements"
                      .Args(Thread.CurrentThread.ManagedThreadId, toRead));
                  for (var j = 0; j < toRead && list.Count > 0; j++)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];
                    table = element.Item1 == 0 ? tA : tB;
                    var buf = table.Get(element.Item2) as byte[];
                    if (buf == null)
                    {
                      list.RemoveAt(idx);
                      continue;
                    }
                    Aver.AreEqual(element.Item4, buf[0]);
                    Aver.AreEqual(element.Item5, buf[element.Item3]);
                  }

                }

                if (i == Int32.MaxValue)
                  i = 0;
                else
                  i++;

                if (list.Count == Int32.MaxValue)
                  list = new List<Tuple<int, GDID, int, byte, byte>>();
              }

              Console.WriteLine("Thread {0} is doing final read of {1} elements"
                .Args(Thread.CurrentThread.ManagedThreadId, list.Count));
              foreach (var element in list)
              {
                var table = element.Item1 == 0 ? tA : tB;
                var buf = table.Get(element.Item2) as byte[];
                if (buf == null)
                  continue;
                Aver.AreEqual(element.Item4, buf[0]);
                Aver.AreEqual(element.Item5, buf[element.Item3]);
              }
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    [Run("speed=true   durationSec=30  putMin=100  putMax=200  delFactor=4  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    [Run("speed=false  durationSec=30  putMin=100  putMax=200  delFactor=4  payloadSizeMin=2  payloadSizeMax=1000  isParallel=true")]
    public void DeleteSeveral_TwoTables_ByteArray(bool speed, int durationSec, int putMin, int putMax, int delFactor, int payloadSizeMin, int payloadSizeMax, bool isParallel)
    {
      using (var cache = new LocalCache(NOPApplication.Instance))
      using (var pile = new DefaultPile(cache))
      {
        cache.Pile = pile;
        cache.PileAllocMode = speed ? AllocationMode.FavorSpeed : AllocationMode.ReuseSpace;
        cache.Start();

        var startTime = DateTime.UtcNow;
        var tasks = new List<Task>();
        for (var t = 0; t < (isParallel ? (System.Environment.ProcessorCount - 1) : 1); t++)
          tasks.Add(Task.Factory.StartNew(() =>
            {
              var list = new List<Tuple<int, GDID, int, byte, byte>>();
              var tA = cache.GetOrCreateTable<GDID>("A");
              var tB = cache.GetOrCreateTable<GDID>("B");
              ulong k = 0;
              var wlc = 0;

              while (true)
              {
                if ((DateTime.UtcNow - startTime).TotalSeconds >= durationSec) break;

                var putCount = Ambient.Random.NextScaledRandomInteger(putMin, putMax);
                for (int i = 0; i < putCount; i++)
                {
                  var payloadSize = Ambient.Random.NextScaledRandomInteger(payloadSizeMin, payloadSizeMax);
                  var val = new byte[payloadSize];
                  val[0] = (byte)Ambient.Random.NextRandomInteger;
                  val[payloadSize - 1] = (byte)Ambient.Random.NextRandomInteger;

                  var tableId = Ambient.Random.NextScaledRandomInteger(0, 1);
                  var table = tableId == 0 ? tA : tB;
                  var key = new GDID((uint)Thread.CurrentThread.ManagedThreadId, k);

                  table.Put(key, val);

                  list.Add(new Tuple<int, GDID, int, byte, byte>(tableId, key, payloadSize - 1, val[0], val[payloadSize - 1]));
                  k++;
                }

                int delCount = putCount / delFactor;
                for (int i = 0; i < delCount; i++)
                {
                  while (true && list.Count > 0)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];
                    var table = element.Item1 == 0 ? tA : tB;
                    var key = element.Item2;

                    var removed = table.Remove(key);
                    list.RemoveAt(idx);
                    if (removed)
                      break;
                  }
                }

                // get several random elements
                if (list.Count > 64 && Ambient.Random.NextScaledRandomInteger(0, 100) > 98)
                {
                  var toRead = Ambient.Random.NextScaledRandomInteger(8, 64);
                  wlc++;
                  if (wlc % 125 == 0)
                    Console.WriteLine("Thread {0} is reading {1} elements"
                      .Args(Thread.CurrentThread.ManagedThreadId, toRead));
                  for (var j = 0; j < toRead && list.Count > 0; j++)
                  {
                    var idx = Ambient.Random.NextScaledRandomInteger(0, list.Count - 1);
                    var element = list[idx];
                    var table = element.Item1 == 0 ? tA : tB;
                    var key = element.Item2;

                    var buf = table.Get(key) as byte[];
                    if (buf == null)
                    {
                      list.RemoveAt(idx);
                      continue;
                    }
                    Aver.AreEqual(element.Item4, buf[0]);
                    Aver.AreEqual(element.Item5, buf[element.Item3]);
                  }
                }

                if (list.Count == Int32.MaxValue)
                  list = new List<Tuple<int, GDID, int, byte, byte>>();
              }

              // total check
              Console.WriteLine("Thread {0} is doing final read of {1} elements".Args(Thread.CurrentThread.ManagedThreadId, list.Count));
              foreach (var element in list)
              {
                var table = element.Item1 == 0 ? tA : tB;
                var val = table.Get(element.Item2) as byte[];
                if (val == null)
                  continue;
                Aver.AreEqual(element.Item4, val[0]);
                Aver.AreEqual(element.Item5, val[element.Item3]);
              }
              return;
            }, TaskCreationOptions.LongRunning));
        Task.WaitAll(tasks.ToArray());
      }
    }

    public struct CheckByteArray
    {
      public CheckByteArray(GDID key, int il, byte fb, byte lb)
      {
        Key = key;
        IdxLast = il;
        FirstByte = fb;
        LastByte = lb;
      }
      public readonly GDID Key;
      public readonly int IdxLast;
      public readonly byte FirstByte;
      public readonly byte LastByte;
    }
  }
}
