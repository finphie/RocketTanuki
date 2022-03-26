using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RocketTanuki
{
    public ref struct Mover
    {
        public Mover(Position position, Move move)
        {
            this.position = position;
            this.move = move;
            position.DoMove(move);
        }

        public bool IsValid()
        {
            return !position.IsChecked(position.SideToMove.ToOpponent());
        }

        private Position position;
        private Move move;

        public void Dispose()
        {
            position.UndoMove(move);
        }
    }
}
