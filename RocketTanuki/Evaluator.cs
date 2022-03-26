﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static System.Math;

namespace RocketTanuki
{
    public sealed class Evaluator
    {
        /// <summary>
        /// P特徴量のインデックス
        /// </summary>
        private enum PieceId
        {
            PieceIdZero = 0,
            FriendHandPawn = PieceIdZero + 1,
            EnemyHandPawn = 20,

            FriendHandLance = 39,
            EnemyHandLance = 44,
            FriendHandKnight = 49,
            EnemyHandKnight = 54,
            FriendHandSilver = 59,
            EnemyHandSilver = 64,
            FriendHandGold = 69,
            EnemyHandGold = 74,
            FriendHandBishop = 79,
            EnemyHandBishop = 82,
            FriendHandRook = 85,
            EnemyHandRook = 88,
            FriendEnemyHandEnd = 90,

            FriendPawn = FriendEnemyHandEnd,
            EnemyPawn = FriendPawn + 81,
            FriendLance = EnemyPawn + 81,
            EnemyLance = FriendLance + 81,
            FriendKnight = EnemyLance + 81,
            EnemyKnight = FriendKnight + 81,
            FriendSilver = EnemyKnight + 81,
            EnemySilver = FriendSilver + 81,
            FriendGold = EnemySilver + 81,
            EnemyGold = FriendGold + 81,
            FriendBishop = EnemyGold + 81,
            EnemyBishop = FriendBishop + 81,
            FriendHorse = EnemyBishop + 81,
            EnemyHorse = FriendHorse + 81,
            FriendRook = EnemyHorse + 81,
            EnemyRook = FriendRook + 81,
            FriendDragon = EnemyRook + 81,
            EnemyDragon = FriendDragon + 81,
            FriendEnemyEnd = EnemyDragon + 81,

            FriendKing = FriendEnemyEnd,
            EnemyKing = FriendKing + Position.BoardSize,
            FriendeEnemyEnd2 = EnemyKing + Position.BoardSize,
        };

        /// <summary>
        /// 盤上の駒のPieceIdのオフセット。
        /// [Piece][先手視点・後手視点]でアクセスする。
        /// </summary>
        private static readonly PieceId[] BoardPieceIds = new PieceId[]{
            PieceId.PieceIdZero,
            PieceId.FriendPawn,
            PieceId.FriendLance,
            PieceId.FriendKnight,
            PieceId.FriendSilver,
            PieceId.FriendGold,
            PieceId.FriendBishop,
            PieceId.FriendRook,
            PieceId.PieceIdZero,
            PieceId.FriendGold,
            PieceId.FriendGold,
            PieceId.FriendGold,
            PieceId.FriendGold,
            PieceId.FriendHorse,
            PieceId.FriendDragon,
            PieceId.EnemyPawn,
            PieceId.EnemyLance,
            PieceId.EnemyKnight,
            PieceId.EnemySilver,
            PieceId.EnemyGold,
            PieceId.EnemyBishop,
            PieceId.EnemyRook,
            PieceId.PieceIdZero,
            PieceId.EnemyGold,
            PieceId.EnemyGold,
            PieceId.EnemyGold,
            PieceId.EnemyGold,
            PieceId.EnemyHorse,
            PieceId.EnemyDragon,
        };

        /// <summary>
        /// 持ち駒のPieceIdのオフセット。
        /// [Piece][先手視点・後手視点]でアクセスする。
        /// </summary>
        private static readonly PieceId[] HandPieceIds = new PieceId[] {
            PieceId.PieceIdZero,
            PieceId.FriendHandPawn,
            PieceId.FriendHandLance,
            PieceId.FriendHandKnight,
            PieceId.FriendHandSilver,
            PieceId.FriendHandGold,
            PieceId.FriendHandBishop,
            PieceId.FriendHandRook,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.EnemyHandPawn,
            PieceId.EnemyHandLance,
            PieceId.EnemyHandKnight,
            PieceId.EnemyHandSilver,
            PieceId.EnemyHandGold,
            PieceId.EnemyHandBishop,
            PieceId.EnemyHandRook,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
            PieceId.PieceIdZero,
        };

        public static Evaluator Instance { get; } = new Evaluator();

        private Evaluator()
        {
            Debug.Assert((int)PieceId.FriendEnemyEnd == 1548);
            Debug.Assert(BoardPieceIds.Length == (int)Piece.NumPieces);
            Debug.Assert(HandPieceIds.Length == (int)Piece.NumPieces);
        }

        public void Load(Dictionary<string, string> options)
        {
            Debug.Assert(options.ContainsKey(Program.EvalDir));
            var evalFilePath = Path.Combine(options[Program.EvalDir], "nn.bin");

            Console.WriteLine("info string Loading an eval file...");
            Console.WriteLine($"info string EvalFile={evalFilePath}");
            Console.Out.Flush();

            using (var reader = new BinaryReader(File.Open(evalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                var version = reader.ReadUInt32();
                Debug.Assert(version == 2062757654, $"Unsupported version: version={version}");

                var hashValue = reader.ReadUInt32();
                Debug.Assert(hashValue == 1046128366, $"Unsupported hash value: hashValue={hashValue}");

                var size = reader.ReadUInt32();
                var architecture = reader.ReadBytes((int)size);

                // 入力層と隠れ層第1層の間のネットワークパラメーター
                // feature_transformer
                var featureTransformerHeader = reader.ReadUInt32();
                Debug.Assert(featureTransformerHeader == 1567217592, $"Unsupported feature transformer header: featureTransformerHeader={featureTransformerHeader}");
                for (int i = 0; i < featureTransformerBiases.Length; ++i)
                {
                    featureTransformerBiases[i] = reader.ReadInt16();
                }
                for (int i = 0; i < featureTransformerWeights.Length; ++i)
                {
                    featureTransformerWeights[i] = reader.ReadInt16();
                }

                // 隠れ層第1層と隠れ層第2層の間のネットワークパラメーター
                var networkHeader = reader.ReadUInt32();
                Debug.Assert(networkHeader == 1664315734, $"Unsupported network header: networkHeader={networkHeader}");
                for (int i = 0; i < firstBiases.Length; ++i)
                {
                    firstBiases[i] = reader.ReadInt32();
                }
                for (int i = 0; i < firstWeights.Length; ++i)
                {
                    firstWeights[i] = reader.ReadSByte();
                }

                // 隠れ層第2層と隠れ層第3層の間のネットワークパラメーター
                for (int i = 0; i < secondBiases.Length; ++i)
                {
                    secondBiases[i] = reader.ReadInt32();
                }
                for (int i = 0; i < secondWeights.Length; ++i)
                {
                    secondWeights[i] = reader.ReadSByte();
                }
                // 隠れ層第3層と出力層の間のネットワークパラメーター
                for (int i = 0; i < thirdBiases.Length; ++i)
                {
                    thirdBiases[i] = reader.ReadInt32();
                }
                for (int i = 0; i < thirdWeights.Length; ++i)
                {
                    thirdWeights[i] = reader.ReadSByte();
                }

                Debug.Assert(reader.BaseStream.Position == reader.BaseStream.Length);
            }

            Console.WriteLine("info string Loaded an eval file...");
        }

        public unsafe int Evaluate(Position position)
        {
            // 入力層と隠れ層第1層の間のネットワークパラメーター
            UpdateAccumulator(position);

            // ClippedReLU
            Span<byte> a1 = stackalloc byte[HalfDimentions * 2];

            ref var first = ref Unsafe.NullRef<short>();
            ref var second = ref Unsafe.NullRef<short>();

            if (position.SideToMove == Color.Black)
            {
                first = MemoryMarshal.GetArrayDataReference(position.State.Z1Black);
                second = MemoryMarshal.GetArrayDataReference(position.State.Z1White);
            }
            else
            {
                first = MemoryMarshal.GetArrayDataReference(position.State.Z1White);
                second = MemoryMarshal.GetArrayDataReference(position.State.Z1Black);
            }

            ref var a1First = ref MemoryMarshal.GetReference(a1);
            for (nuint i = 0; i < HalfDimentions; ++i)
            {
                Unsafe.Add(ref a1First, i) = (byte)Clamp((int)Unsafe.Add(ref first, i), 0, 127);
                Unsafe.Add(ref a1First, i + HalfDimentions) = (byte)Clamp((int)Unsafe.Add(ref second, i), 0, 127);
            }

            // 隠れ層第1層から隠れ層第2層の間のネットワークパラメーター
            Span<int> z2 = stackalloc int[32];
            fixed (byte* a1Pointer = a1)
            fixed (sbyte* firstWeightsPointer = firstWeights)
            {
                for (int outputIndex = 0; outputIndex < z2.Length; ++outputIndex)
                {
                    int offset = outputIndex * a1.Length;

                    //for (int inputIndex = 0; inputIndex < a1.Length; ++inputIndex)
                    //{
                    //    z2[outputIndex] += firstWeights[offset + inputIndex] * a1[inputIndex];
                    //}

                    var sum = Vector256<int>.Zero;
                    for (int chunkIndex = 0; chunkIndex < HalfDimentions * 2 / Vector256<sbyte>.Count; ++chunkIndex)
                    {
                        var productShort = Avx2.MultiplyAddAdjacent(
                            Avx2.LoadVector256(&a1Pointer[chunkIndex * Vector256<byte>.Count]),
                            Avx2.LoadVector256(&firstWeightsPointer[offset + chunkIndex * Vector256<sbyte>.Count]));
                        var productInt = Avx2.MultiplyAddAdjacent(productShort, Vector256.Create((short)1));
                        sum = Avx2.Add(sum, productInt);
                    }
                    var sum128 = Avx2.Add(sum.GetLower(), sum.GetUpper());
                    sum128 = Avx2.Add(sum128, Avx2.Shuffle(sum128, _MM_PERM_BADC));
                    sum128 = Avx2.Add(sum128, Avx2.Shuffle(sum128, _MM_PERM_CDAB));
                    z2[outputIndex] = sum128.ToScalar() + firstBiases[outputIndex];
                }
            }

            Span<byte> a2 = stackalloc byte[32];
            ref var a2First = ref MemoryMarshal.GetReference(a2);
            for (int outputIndex = 0; outputIndex < z2.Length; ++outputIndex)
            {
                Unsafe.Add(ref a2First, (nint)(uint)outputIndex) = (byte)Clamp(z2[outputIndex] >> WeightScaleBits, 0, 127);
            }

            // 隠れ層第2層から隠れ層第3層の間のネットワークパラメーター
            Span<int> z3 = stackalloc int[32];
            fixed (byte* a2Pointer = a2)
            fixed (sbyte* secondWeightsPointer = secondWeights)
            {
                for (int outputIndex = 0; outputIndex < z3.Length; ++outputIndex)
                {
                    int offset = outputIndex * a2.Length;

                    //for (int inputIndex = 0; inputIndex < a2.Length; ++inputIndex)
                    //{
                    //    z3[outputIndex] += secondWeights[offset + inputIndex] * a2[inputIndex];
                    //}

                    var sum = Vector256<int>.Zero;
                    for (int chunkIndex = 0; chunkIndex < a2.Length / Vector256<sbyte>.Count; ++chunkIndex)
                    {
                        var productShort = Avx2.MultiplyAddAdjacent(
                            Avx2.LoadVector256(&a2Pointer[chunkIndex * Vector256<byte>.Count]),
                            Avx2.LoadVector256(&secondWeightsPointer[offset + chunkIndex * Vector256<sbyte>.Count]));
                        var productInt = Avx2.MultiplyAddAdjacent(productShort, Vector256.Create((short)1));
                        sum = Avx2.Add(sum, productInt);
                    }
                    var sum128 = Avx2.Add(sum.GetLower(), sum.GetUpper());
                    sum128 = Avx2.Add(sum128, Avx2.Shuffle(sum128, _MM_PERM_BADC));
                    sum128 = Avx2.Add(sum128, Avx2.Shuffle(sum128, _MM_PERM_CDAB));
                    z3[outputIndex] = sum128.ToScalar() + secondBiases[outputIndex];
                }
            }

            Span<int> a3 = stackalloc int[32];
            ref var a3First = ref MemoryMarshal.GetReference(a3);
            for (int outputIndex = 0; outputIndex < z3.Length; ++outputIndex)
            {
                Unsafe.Add(ref a3First, (nint)(uint)outputIndex) = Max(0, Min(127, z3[outputIndex] >> WeightScaleBits));
            }

            // 隠れ層第3層から出力層の間のネットワークパラメーター
            var z4 = MemoryMarshal.GetArrayDataReference(thirdBiases);
            ref var thirdWeightsFirst = ref MemoryMarshal.GetArrayDataReference(thirdWeights);
            for (int inputIndex = 0; inputIndex < a3.Length; ++inputIndex)
            {
                z4 += Unsafe.Add(ref thirdWeightsFirst, (nint)(uint)inputIndex) * a3[inputIndex];
            }

            return z4 / FVScale;
        }

        /// <summary>
        /// Accumulatorの値を計算する。
        /// </summary>
        /// <param name="position"></param>
        public void UpdateAccumulator(Position position)
        {
            if (position.State.Z1Black != null && position.State.Z1White != null)
            {
                // すでに計算してあるので何もしない。
                return;
            }

            if (position.State.Previous != null
                && position.State.Previous.Z1Black != null
                && position.State.Previous.Z1White != null
                && position.LastMove.PieceFrom != Piece.BlackKing
                && position.LastMove.PieceFrom != Piece.WhiteKing)
            {
                // 一つ前の局面のAccumulatorの値を計算する。
                UpdateAccumulatorIncrementally(position);
                return;
            }

            // 一つ前の局面のAccumulatorの値が使えないため、全計算を行う。
            // TODO(nodchip): 玉が移動した場合は片側のみ全計算を行う。
            UpdateAccumulatorFully(position);
        }

        /// <summary>
        /// Accumulatorの値を全計算で計算する。
        /// </summary>
        /// <param name="position"></param>
        private void UpdateAccumulatorFully(Position position)
        {
            // 全計算
            position.State.Z1Black = new short[HalfDimentions];
            position.State.Z1White = new short[HalfDimentions];

            // バイアスベクトルをコピーする
            Array.Copy(featureTransformerBiases, position.State.Z1Black, HalfDimentions);
            Array.Copy(featureTransformerBiases, position.State.Z1White, HalfDimentions);

            // 盤上の駒
            for (int file = 0; file < Position.BoardSize; ++file)
            {
                for (int rank = 0; rank < Position.BoardSize; ++rank)
                {
                    ref var board = ref position.Board[file, rank];

                    if (board is Piece.NoPiece or Piece.BlackKing or Piece.WhiteKing)
                    {
                        continue;
                    }

                    Add(position,
                        MakeBoardPieceId(board, file, rank),
                        MakeBoardPieceId(board.AsOpponentPiece(), 8 - file, 8 - rank));
                }
            }

            // 持ち駒
            ref var handPiecesFirst = ref MemoryMarshal.GetArrayDataReference(position.HandPieces);
            for (var handPiece = Piece.NoPiece; handPiece < Piece.NumPieces; ++handPiece)
            {
                for (int numHandPieces = 1; numHandPieces <= Unsafe.Add(ref handPiecesFirst, (nint)(uint)handPiece); ++numHandPieces)
                {
                    Add(position,
                        MakeHandPieceId(handPiece, numHandPieces),
                        MakeHandPieceId(handPiece.AsOpponentPiece(), numHandPieces));
                }
            }
        }

        /// <summary>
        /// Accumulatorの値を差分計算で計算する。
        /// </summary>
        /// <param name="position"></param>
        private void UpdateAccumulatorIncrementally(Position position)
        {
            Debug.Assert(position.State.Previous != null);
            Debug.Assert(position.State.Previous.Z1Black != null);
            Debug.Assert(position.State.Previous.Z1White != null);
            Debug.Assert(position.LastMove.PieceFrom != Piece.BlackKing);
            Debug.Assert(position.LastMove.PieceFrom != Piece.WhiteKing);
            Debug.Assert(position.LastMove.PieceTo != Piece.BlackKing);
            Debug.Assert(position.LastMove.PieceTo != Piece.WhiteKing);

            // 差分計算
            Debug.Assert(position.LastMove != Move.None);

            position.State.Z1Black = new short[HalfDimentions];
            position.State.Z1White = new short[HalfDimentions];

            // 1手前のベクトルをコピーする
            Array.Copy(position.State.Previous.Z1Black, position.State.Z1Black, HalfDimentions);
            Array.Copy(position.State.Previous.Z1White, position.State.Z1White, HalfDimentions);

            ref var move = ref position.LastMove;
            if (move.PieceTo != Piece.NoPiece)
            {
                // 相手の駒を取る指し手

                // 持ち駒を増やす
                var handPiece = move.PieceTo.AsOpponentHandPiece();
                var numHandPieces = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(position.HandPieces), (nint)(uint)handPiece);
                Add(position,
                    MakeHandPieceId(handPiece, numHandPieces),
                    MakeHandPieceId(handPiece.AsOpponentPiece(), numHandPieces));

                // 盤上から駒を取り除く
                Subtract(position,
                    MakeBoardPieceId(move.PieceTo, move.FileTo, move.RankTo),
                    MakeBoardPieceId(move.PieceTo.AsOpponentPiece(), 8 - move.FileTo, 8 - move.RankTo));
            }

            // 盤上に駒を置く
            Add(position,
                MakeBoardPieceId(move.PieceFrom, move.FileTo, move.RankTo),
                MakeBoardPieceId(move.PieceFrom.AsOpponentPiece(), 8 - move.FileTo, 8 - move.RankTo));

            if (move.Drop)
            {
                // 駒を打つ指し手

                // 持ち駒を減らす
                var handPiece = move.PieceFrom;
                var numHandPieces = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(position.HandPieces), (nint)(uint)handPiece);
                Subtract(position,
                    MakeHandPieceId(handPiece, numHandPieces),
                    MakeHandPieceId(handPiece.AsOpponentPiece(), numHandPieces));
            }
            else
            {
                // 駒を移動する指し手
                Subtract(position,
                    MakeBoardPieceId(move.PieceFrom, move.FileFrom, move.RankFrom),
                    MakeBoardPieceId(move.PieceFrom.AsOpponentPiece(), 8 - move.FileFrom, move.RankFrom));
            }
        }

        /// <summary>
        /// 特徴ベクトルをアフィン変換し保持してあるベクトルに、重み行列の列を足す。
        /// </summary>
        /// <param name="position"></param>
        /// <param name="pieceIdFromBlack"></param>
        /// <param name="pieceIdFromWhite"></param>
        private unsafe void Add(Position position, int pieceIdFromBlack, int pieceIdFromWhite)
        {
            fixed (short* featureTransformerWeightsPointer = featureTransformerWeights)
            fixed (short* z1BlackPointer = position.State.Z1Black)
            fixed (short* z1WhitePointer = position.State.Z1White)
            {
                int kpIndexBlack = MakeKPIndex(position.BlackKingFile, position.BlackKingRank, pieceIdFromBlack);
                int offsetBlack = HalfDimentions * kpIndexBlack;
                int kpIndexWhite = MakeKPIndex(8 - position.WhiteKingFile, 8 - position.WhiteKingRank, pieceIdFromWhite);
                int offsetWhite = HalfDimentions * kpIndexWhite;

                //for (int j = 0; j < HalfDimentions; ++j)
                //{
                //    z1Black[j] += featureTransformerWeights[offsetBlack + j];
                //    z1White[j] += featureTransformerWeights[offsetWhite + j];
                //}

                for (int chunkIndex = 0; chunkIndex < HalfDimentions / Vector256<short>.Count; ++chunkIndex)
                {
                    Avx2.Store(&z1BlackPointer[chunkIndex * Vector256<short>.Count], Avx2.Add(
                        Avx2.LoadVector256(&z1BlackPointer[chunkIndex * Vector256<short>.Count]),
                        Avx2.LoadVector256(&featureTransformerWeightsPointer[offsetBlack + chunkIndex * Vector256<short>.Count])));
                    Avx2.Store(&z1WhitePointer[chunkIndex * Vector256<short>.Count], Avx2.Add(
                        Avx2.LoadVector256(&z1WhitePointer[chunkIndex * Vector256<short>.Count]),
                        Avx2.LoadVector256(&featureTransformerWeightsPointer[offsetWhite + chunkIndex * Vector256<short>.Count])));
                }
            }
        }

        /// <summary>
        /// 特徴ベクトルをアフィン変換し保持してあるベクトルから、重み行列の列を引く。
        /// </summary>
        /// <param name="position"></param>
        /// <param name="pieceIdFromBlack"></param>
        /// <param name="pieceIdFromWhite"></param>
        private unsafe void Subtract(Position position, int pieceIdFromBlack, int pieceIdFromWhite)
        {
            fixed (short* featureTransformerWeightsPointer = featureTransformerWeights)
            fixed (short* z1BlackPointer = position.State.Z1Black)
            fixed (short* z1WhitePointer = position.State.Z1White)
            {
                int kpIndexBlack = MakeKPIndex(position.BlackKingFile, position.BlackKingRank, pieceIdFromBlack);
                int offsetBlack = HalfDimentions * kpIndexBlack;
                int kpIndexWhite = MakeKPIndex(8 - position.WhiteKingFile, 8 - position.WhiteKingRank, pieceIdFromWhite);
                int offsetWhite = HalfDimentions * kpIndexWhite;

                //for (int j = 0; j < HalfDimentions; ++j)
                //{
                //    z1Black[j] += featureTransformerWeights[offsetBlack + j];
                //    z1White[j] += featureTransformerWeights[offsetWhite + j];
                //}

                for (int chunkIndex = 0; chunkIndex < HalfDimentions / Vector256<short>.Count; ++chunkIndex)
                {
                    Avx2.Store(&z1BlackPointer[chunkIndex * Vector256<short>.Count], Avx2.Subtract(
                        Avx2.LoadVector256(&z1BlackPointer[chunkIndex * Vector256<short>.Count]),
                        Avx2.LoadVector256(&featureTransformerWeightsPointer[offsetBlack + chunkIndex * Vector256<short>.Count])));
                    Avx2.Store(&z1WhitePointer[chunkIndex * Vector256<short>.Count], Avx2.Subtract(
                        Avx2.LoadVector256(&z1WhitePointer[chunkIndex * Vector256<short>.Count]),
                        Avx2.LoadVector256(&featureTransformerWeightsPointer[offsetWhite + chunkIndex * Vector256<short>.Count])));
                }
            }
        }
        /// <summary>
        /// 盤上の駒のPieceIdを計算する。
        /// </summary>
        /// <param name="piece"></param>
        /// <param name="file"></param>
        /// <param name="rank"></param>
        /// <returns></returns>
        private static int MakeBoardPieceId(Piece piece, int file, int rank)
        {
            int square = file * Position.BoardSize + rank;
            PieceId offset = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(BoardPieceIds), (nint)(uint)piece);
            Debug.Assert(offset != PieceId.PieceIdZero);
            return (int)offset + square;
        }

        /// <summary>
        /// 持ち駒のPieceIdを計算する
        /// </summary>
        /// <param name="piece"></param>
        /// <param name="numHandPieces"></param>
        /// <returns></returns>
        private static int MakeHandPieceId(Piece piece, int numHandPieces)
        {
            PieceId offset = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(HandPieceIds), (nint)(uint)piece);
            Debug.Assert(offset != PieceId.PieceIdZero);
            // 1枚目の持ち駒のIDは0から始まるので、1引く
            return (int)offset + numHandPieces - 1;
        }

        /// <summary>
        /// KP特徴量インデックスを計算する。
        /// </summary>
        /// <param name="kingFile"></param>
        /// <param name="kingRank"></param>
        /// <param name="pieceId"></param>
        /// <returns></returns>
        private static int MakeKPIndex(int kingFile, int kingRank, int pieceId)
        {
            return (kingFile * Position.BoardSize + kingRank) * (int)PieceId.FriendEnemyEnd + pieceId;
        }

        /// <summary>
        /// 詰みのスコアを返す。
        /// </summary>
        /// <param name="play">Rootノードからの手数</param>
        /// <returns></returns>
        public static int MateIn(int play)
        {
            return MateValue - play;
        }

        /// <summary>
        /// 待たされたときのスコアを返す。
        /// </summary>
        /// <param name="play">Rootノードからの手数</param>
        /// <returns></returns>
        public static int MatedIn(int play)
        {
            return -MateValue + play;
        }

        public const int ZeroValue = 0;
        public const int MateValue = 32000;
        public const int InfiniteValue = 32001;
        public const int InvalidValue = 32002;
        public const int MaxPlay = 128;
        public const int MateInMaxPlayValue = MateValue - MaxPlay;
        public const int MatedInMaxPlayValue = -MateValue + MaxPlay;
        public const int DrawValue = -1;

        public const int PawnValue = 90;
        public const int LanceValue = 315;
        public const int KnightValue = 405;
        public const int SilverValue = 495;
        public const int GoldValue = 540;
        public const int BishopValue = 855;
        public const int RookValue = 990;
        public const int ProPawnValue = 540;
        public const int ProLanceValue = 540;
        public const int ProKnightValue = 540;
        public const int ProSilverValue = 540;
        public const int HorseValue = 945;
        public const int DragonValue = 1395;
        public const int KingValue = 15000;
        private const int HalfDimentions = 256;
        private const int WeightScaleBits = 6;
        private const int FVScale = 16;
        private const int _MM_PERM_BADC = 0x4E;
        private const int _MM_PERM_CDAB = 0xB1;

        private static readonly int[] MaterialValues = {
            ZeroValue,
            PawnValue,
            LanceValue,
            KnightValue,
            SilverValue,
            GoldValue,
            BishopValue,
            RookValue,
            KingValue,
            ProPawnValue,
            ProLanceValue,
            ProKnightValue,
            ProSilverValue,
            HorseValue,
            DragonValue,
            -PawnValue,
            -LanceValue,
            -KnightValue,
            -SilverValue,
            -GoldValue,
            -BishopValue,
            -RookValue,
            -KingValue,
            -ProPawnValue,
            -ProLanceValue,
            -ProKnightValue,
            -ProSilverValue,
            -HorseValue,
            -DragonValue,
            InvalidValue,
        };
        private readonly short[] featureTransformerBiases = new short[HalfDimentions];
        private readonly short[] featureTransformerWeights = new short[HalfDimentions * 125388];
        private readonly int[] firstBiases = new int[32];
        private readonly sbyte[] firstWeights = new sbyte[32 * HalfDimentions * 2];
        private readonly int[] secondBiases = new int[32];
        private readonly sbyte[] secondWeights = new sbyte[32 * 32];
        private readonly int[] thirdBiases = new int[1];
        private readonly sbyte[] thirdWeights = new sbyte[1 * 32];
    }
}
