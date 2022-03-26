using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RocketTanuki
{
    public enum Bound
    {
        None = 0,
        Upper = 1,
        Lower = 2,
        Exact = 3,
    }

    public struct TranspositionTableEntry
    {
        public ulong Hash;
        public ushort Move;
        public sbyte Depth;
        public byte Bound;
        public ushort Generation;
        public short Value;
    }

    public unsafe sealed class TranspositionTable
    {
        public static TranspositionTable Instance { get; } = new TranspositionTable();

        TranspositionTableEntry* Entries = (TranspositionTableEntry*)Unsafe.AsPointer(ref Unsafe.NullRef<TranspositionTableEntry>());
        ulong length;

        public void Resize(int hashSizeMb)
        {
            if (!Unsafe.IsNullRef(ref Unsafe.AsRef<TranspositionTableEntry>(Entries)))
            {
                NativeMemory.Free(Entries);
            }

            var size = checked((ulong)hashSizeMb * 1024 * 1024);
            length = size / (uint)Unsafe.SizeOf<TranspositionTableEntry>();
            Entries = (TranspositionTableEntry*)NativeMemory.Alloc(checked((nuint)length), (nuint)Unsafe.SizeOf<TranspositionTableEntry>());
        }

        public void NewSearch()
        {
            generation = (generation + 1) & 0xffff;
        }

        public void Save(ulong hash, int value, int depth, Move move, Bound bound)
        {
            ulong mask = length - 1;
            ulong index = hash & mask;

            ref var entry = ref Entries[index];

            if (bound == Bound.Exact
                || hash != entry.Hash
                || entry.Depth < depth)
            {

                entry.Hash = hash;
                entry.Move = move.ToUshort();
                entry.Depth = (sbyte)depth;
                entry.Bound = (byte)bound;
                entry.Generation = (ushort)generation;
                entry.Value = (short)value;
            }
        }

        public TranspositionTableEntry Probe(ulong hash, out bool found)
        {
            ulong mask = length - 1;
            ulong index = hash & mask;

            ref var entry = ref Entries[index];
            found = entry.Hash == hash;

            return entry;
        }

        private int generation;
    }
}
