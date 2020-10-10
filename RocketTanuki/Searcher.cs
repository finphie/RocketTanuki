﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static RocketTanuki.Evaluator;

namespace RocketTanuki
{
    public class Searcher
    {
        public Searcher(int threadId)
        {
            this.threadId = threadId;
        }

        public BestMove Search(Position position)
        {
            BestMove bestMove = new BestMove
            {
                Move = Move.Resign,
                Depth = 1,
            };
            NumSearchedNodes = 0;

            for (int depth = 1; depth < MaxPlay && Searchers.Instance.thinking; ++depth)
            {
                int alpha = -InfiniteValue;
                int beta = InfiniteValue;
                int delta = InfiniteValue;

                if (depth >= 4)
                {
                    delta = 17;
                    alpha = Math.Max(bestMove.Value - delta, -InfiniteValue);
                    beta = Math.Min(bestMove.Value + delta, InfiniteValue);
                }

                BestMove bestMoveCandidate;
                while (true)
                {
                    bestMoveCandidate = search(position, alpha, beta, depth);
                    bestMoveCandidate.Depth = depth;
                    if ((bestMoveCandidate.Value <= alpha || beta <= bestMoveCandidate.Value)
                        && TimeManager.Instance.ElapsedMs() > 3000)
                    {
                        Usi.OutputPv(bestMoveCandidate, alpha, beta);
                    }

                    if (!Searchers.Instance.thinking)
                    {
                        break;
                    }

                    if (bestMoveCandidate.Value <= alpha)
                    {
                        beta = (alpha + beta) / 2;
                        alpha = Math.Max(bestMoveCandidate.Value - delta, -InfiniteValue);
                    }
                    else if (beta <= bestMoveCandidate.Value)
                    {
                        beta = Math.Min(bestMoveCandidate.Value + delta, InfiniteValue);
                    }
                    else
                    {
                        break;
                    }

                    delta += delta / 4 + 5;

                    Debug.Assert(-InfiniteValue <= alpha);
                    Debug.Assert(beta <= InfiniteValue);
                }

                if (Searchers.Instance.thinking)
                {
                    bestMove = bestMoveCandidate;
                }

                Usi.OutputPv(bestMove, -InfiniteValue, InfiniteValue);
            }
            return bestMove;
        }

        /// <summary>
        /// Alpha-Beta - Chessprogramming wiki https://www.chessprogramming.org/Alpha-Beta
        /// </summary>
        /// <param name="position"></param>
        /// <param name="alpha"></param>
        /// <param name="beta"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        private BestMove search(Position position, int alpha, int beta, int depth)
        {
            if (!Searchers.Instance.thinking)
            {
                return new BestMove
                {
                    Value = DrawValue,
                };
            }

            if (threadId == 0 && (NumSearchedNodes++) % 4096 == 0)
            {
                // TimeManager.IsThinking()は重いと思うので、
                // 定期的に結果を確認し、Searchers.thinkingに代入する。
                if (!TimeManager.Instance.IsThinking())
                {
                    Searchers.Instance.thinking = false;
                }
            }

            if (depth == 0)
            {
                // ゲーム木の末端に到達したので、局面を評価し、返す。
                return new BestMove
                {
                    Value = Evaluate(position),
                };
            }

            BestMove bestChildBestMove = null;
            Move bestMove = Move.Resign;
            bool searchPv = true;
            foreach (var move in MoveGenerator.Generate(position))
            {
                if (!Searchers.Instance.thinking)
                {
                    break;
                }

                BestMove childBestMove;
                using (var mover = new Mover(position, move))
                {
                    if (!mover.IsValid())
                    {
                        // 王手を放置しているので、処理しない。
                        continue;
                    }

                    if (searchPv)
                    {
                        childBestMove = search(position, -beta, -alpha, depth - 1);
                    }
                    else
                    {
                        childBestMove = search(position, -alpha - 1, -alpha, depth - 1);
                        if (-childBestMove.Value > alpha)
                        {
                            childBestMove = search(position, -beta, -alpha, depth - 1);
                        }
                    }
                }

                if (-childBestMove.Value >= beta)
                {
                    return new BestMove
                    {
                        Value = beta, //  fail hard beta-cutoff
                        Move = move,
                        Next = childBestMove,
                    };
                }

                if (-childBestMove.Value > alpha)
                {
                    alpha = -childBestMove.Value; // alpha acts like max in MiniMax
                    bestMove = move;
                    bestChildBestMove = childBestMove;
                    searchPv = false;
                }
            }

            var result = new BestMove
            {
                Move = bestMove,
                Next = bestChildBestMove,
            };
            if (bestMove == Move.Resign)
            {
                // 合法手が存在しなかった=負け
                result.Value = MatedIn(1);
            }
            else if (alpha < MatedInMaxPlayValue)
            {
                result.Value = alpha + 1;
            }
            else if (MateInMaxPlayValue < alpha)
            {
                result.Value = alpha - 1;
            }
            else
            {
                result.Value = alpha;
            }
            return result;
        }

        private int threadId;
        public long NumSearchedNodes { get; set; } = 0;
    }
}
