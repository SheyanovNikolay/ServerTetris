using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace server
{
    class Shape
    {
        public int x;
        public int y;
        public int[,] matrix;
        public int[,] nextMatrix;
        public int sizeMatrix;
        public int sizeNextMatrix;

        public int[,] shapeI = new int[4, 4]{
            {0, 0, 1, 0},
            {0, 0, 1, 0},
            {0, 0, 1, 0},
            {0, 0, 1, 0},
        };

        public int[,] shapeS = new int[3, 3]{
            {0, 2, 0},
            {0, 2, 2},
            {0, 0, 2},
        };

        public int[,] shapeZ = new int[3, 3]{
            {0,3,0},
            {3,3,0},
            {3,0,0},
        };

        public int[,] shapeL = new int[3, 3]{
            {4, 0, 0},
            {4, 0, 0},
            {4, 4, 0},
        };

        public int[,] shapeJ = new int[3, 3]{
            {0, 0, 5},
            {0, 0, 5},
            {0, 5, 5},
        };

        public int[,] shapeO = new int[2, 2]{
            {6, 6},
            {6, 6},
        };
        public int[,] shapeT = new int[3, 3]{
            {7, 0, 0},
            {7, 7, 0},
            {7, 0, 0},
        };


        public Shape(int _x, int _y)
        {
            x = _x;
            y = _y;
            matrix = GenerateMatrix().First();
            sizeMatrix = (int)Math.Sqrt(matrix.Length);
            nextMatrix = GenerateMatrix().First();
            sizeNextMatrix = (int)Math.Sqrt(nextMatrix.Length);
        }

        public void ResetShape(int _x, int _y)
        {
            x = _x;
            y = _y;
            matrix = nextMatrix;
            sizeMatrix = (int)Math.Sqrt(matrix.Length);
            nextMatrix = GenerateMatrix().First();
            sizeNextMatrix = (int)Math.Sqrt(nextMatrix.Length);
        }

        public IEnumerable<int[,]> GenerateMatrix()
        {
            var pieces = new List<int[,]> {shapeI, shapeL, shapeJ, shapeS, shapeZ, shapeT, shapeO}; //Сет всех тетрамино
            var order = new List<int[,]> {shapeI, shapeL, shapeJ, shapeT};
            var history = new List<int[,]> {shapeS, shapeZ, shapeS}; //Пул истории фигур

            var pool = new List<int[,]>(); //Пул фигур

            for (int i = 0; i < 5; i++)
                pool.Concat(pieces);

            Random randomizer = new Random();

            //Первый элемент выкидывается по более простому правилу
            var firstElement = order.ElementAt(randomizer.Next(0, 4));
            yield return firstElement;
            history.Add(firstElement);

            order.Clear();

            while (true)
            {
                int i = 0;
                var piece = shapeI;

                //Роллим фигурку
                for (int roll = 0; roll < 6; ++roll)
                {
                    i = randomizer.Next(0, 36);
                    piece = pool.ElementAt(i);
                    if (history.Contains(piece) || roll == 5) {
                        break;
                    }
                    if (order.Count == 0)
                        pool[i] = order[0];
                }

                //Правим порядок броска фигур, чтобы не было потопов/засух
                if (order.Contains(piece))
                {
                    order.RemoveRange(order.IndexOf(piece), 1);
                }
                order.Add(piece);

                pool[i] = order[0];

                //Добавляем полученую в историю
                history.ShiftLeft(1);
                history[3] = piece;
                yield return piece;
            }
        }

        public void RotateShape()
        {
            int[,] tempMatrix = new int[sizeMatrix, sizeMatrix];
            for (int i = 0; i < sizeMatrix; i++)
            {
                for (int j = 0; j < sizeMatrix; j++)
                {
                    tempMatrix[i, j] = matrix[j, (sizeMatrix - 1) - i];
                }
            }
            matrix = tempMatrix;
            int offset1 = (8 - (x + sizeMatrix));
            if (offset1 < 0)
            {
                for (int i = 0; i < Math.Abs(offset1); i++)
                    MoveLeft();
            }

            if (x < 0)
            {
                for (int i = 0; i < Math.Abs(x) + 1; i++)
                    MoveRight();
            }

        }

        public void MoveDown()
        {
            y++;
        }
        public void MoveRight()
        {
            x++;
        }
        public void MoveLeft()
        {
            x--;
        }
    }

    public static class ShiftList
    {
        public static List<T> ShiftLeft<T>(this List<T> list, int shiftBy)
        {
            if (list.Count <= shiftBy)
            {
                return list;
            }

            var result = list.GetRange(shiftBy, list.Count - shiftBy);
            result.AddRange(list.GetRange(0, shiftBy));
            return result;
        }

        public static List<T> ShiftRight<T>(this List<T> list, int shiftBy)
        {
            if (list.Count <= shiftBy)
            {
                return list;
            }

            var result = list.GetRange(list.Count - shiftBy, shiftBy);
            result.AddRange(list.GetRange(0, list.Count - shiftBy));
            return result;
        }
    }
}
