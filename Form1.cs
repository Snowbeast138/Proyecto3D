using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;

namespace Proyecto3D
{
    public partial class Form1 : Form
    {
        private Timer gameTimer;

        private List<Slime> slimes;

        private Point lastMousePos;

        private Slime draggedSlime = null;

        private bool mouseMoved = false;

        public Form1()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(800, 600);
            this.Text = "Slime 3D";
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.KeyPreview = true;

            slimes = new List<Slime> { new Slime(0, -100, 300, 80) };

            gameTimer = new Timer { Interval = 16 };
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            this.MouseDown += Form1_MouseDown;
            this.MouseMove += Form1_MouseMove;
            this.MouseUp += Form1_MouseUp;
            this.KeyDown += Form1_KeyDown;
        }

        private void GameLoop(object sender, EventArgs e)
        {
            for (int i = 0; i < slimes.Count; i++)
            {
                bool isBeingDragged = (slimes[i] == draggedSlime);
                slimes[i].UpdatePhysics(isBeingDragged);

                for (int j = i + 1; j < slimes.Count; j++)
                {
                    var s1 = slimes[i];
                    var s2 = slimes[j];

                    float dist = s1.GetDistancia(s2);
                    float distanciaColision = (s1.Size + s2.Size) * 0.6f;

                    if (dist > 5)
                    {
                        s1.AtraerA (s2);
                    }

                    if (dist < distanciaColision)
                    {
                        if (s1.Cooldown <= 0 && s2.Cooldown <= 0)
                        {
                            s1.Fusionar (s2);
                            slimes.RemoveAt (j);

                            if (draggedSlime == s2) draggedSlime = s1;
                            break;
                        }
                    }
                }
            }
            this.Invalidate();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
            {
                foreach (var s in slimes) s.Size *= 1.1f;
            }
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
            {
                foreach (var s in slimes)
                {
                    if (s.Size > 5) s.Size *= 0.9f;
                }
            }
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int cX = this.Width / 2;
                int cY = this.Height / 2;
                draggedSlime = null;
                mouseMoved = false;
                lastMousePos = e.Location;

                foreach (var s in slimes.OrderBy(s => s.Z))
                {
                    float f = 500 / (500 + s.Z);
                    float pX = cX + s.X * f;
                    float pY = cY + s.Y * f;

                    float dist =
                        (float)
                        Math
                            .Sqrt(Math.Pow(pX - e.X, 2) +
                            Math.Pow(pY - e.Y, 2));

                    if (dist < s.Size * f)
                    {
                        draggedSlime = s;
                        break;
                    }
                }
            }
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (draggedSlime != null)
            {
                float dx = e.X - lastMousePos.X;
                float dy = e.Y - lastMousePos.Y;

                if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) mouseMoved = true;

                draggedSlime.Rotate(dx * 0.015f, dy * 0.015f);
                draggedSlime.X += dx;
                draggedSlime.Y += dy;

                lastMousePos = e.Location;
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (
                e.Button == MouseButtons.Left &&
                draggedSlime != null &&
                !mouseMoved
            )
            {
                if (draggedSlime.Size > 20)
                {
                    var hijos = draggedSlime.Dividir();
                    slimes.Remove (draggedSlime);
                    slimes.AddRange (hijos);
                }
            }
            draggedSlime = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cX = this.Width / 2;
            int cY = this.Height / 2;

            foreach (var slime in slimes.OrderByDescending(s => s.Z))
            {
                bool isSelected = (slime == draggedSlime);
                slime.Render(e.Graphics, cX, cY, isSelected);
            }
        }
    }

    public class Slime
    {
        public float

                X,
                Y,
                Z,
                Size;

        public int Cooldown = 0;

        private float

                angleX = 0,
                angleY = 0;

        public float

                vX = 0,
                vY = 0,
                vZ = 0;

        private int jumpTimer = 0;

        private static Random rand = new Random();

        private Color bodyColor = Color.FromArgb(150, 100, 255, 100);

        private Color eyeColor = Color.FromArgb(255, 30, 70, 30);

        public Slime(float x, float y, float z, float size)
        {
            X = x;
            Y = y;
            Z = z;
            Size = size;
            jumpTimer = rand.Next(30, 100);
        }

        public void UpdatePhysics(bool isBeingDragged)
        {
            if (Cooldown > 0) Cooldown--;

            if (isBeingDragged)
            {
                vX = 0;
                vY = 0;
                vZ = 0;
                return;
            }

            // --- 1. RECUPERACIÓN DE POSTURA (LERP) ---
            // Suavemente reducimos los ángulos a 0 para que vuelva a mirar al frente
            angleX += (0 - angleX) * 0.08f;
            angleY += (0 - angleY) * 0.08f;

            // Gravedad
            vY += 0.6f;

            X += vX;
            Y += vY;
            Z += vZ;

            // Fricción general
            vX *= 0.92f;
            vZ *= 0.92f;

            // --- 2. LÍMITES DE PANTALLA (Bordes) ---
            float limiteX = 350; // Límite lateral
            float minZ = 150; // Límite cercanía (frente)
            float maxZ = 650; // Límite profundidad (fondo)
            float factorRebote = -0.7f; // Invertimos velocidad y perdemos un poco de energía

            // Rebote en X
            if (X > limiteX)
            {
                X = limiteX;
                vX *= factorRebote;
            }
            if (X < -limiteX)
            {
                X = -limiteX;
                vX *= factorRebote;
            }

            // Rebote en Z
            if (Z > maxZ)
            {
                Z = maxZ;
                vZ *= factorRebote;
            }
            if (Z < minZ)
            {
                Z = minZ;
                vZ *= factorRebote;
            }

            // Suelo (Eje Y)
            float nivelSuelo = 200;
            float limiteInferior = nivelSuelo - (Size / 2);

            if (Y >= limiteInferior)
            {
                Y = limiteInferior;
                vY = 0;

                jumpTimer--;
                if (jumpTimer <= 0)
                {
                    jumpTimer = rand.Next(40, 120);
                    vY = -rand.Next(10, 18);
                    vX += rand.Next(-10, 11);
                    vZ += rand.Next(-10, 11);
                }
            }
        }

        public void Rotate(float ax, float ay)
        {
            angleY += ax;
            angleX += ay;
        }

        public void AtraerA(Slime otro)
        {
            float ease = 0.03f;
            if (Cooldown <= 0)
            {
                vX += (otro.X - X) * ease;
                vZ += (otro.Z - Z) * ease;
            }
        }

        public float GetDistancia(Slime otro) =>
            (float)
            Math
                .Sqrt(Math.Pow(X - otro.X, 2) +
                Math.Pow(Y - otro.Y, 2) +
                Math.Pow(Z - otro.Z, 2));

        public List<Slime> Dividir()
        {
            float newSize = Size / 1.25992f;

            var hijo1 =
                new Slime(X - Size / 2, Y - 10, Z, newSize) { Cooldown = 120 };
            var hijo2 =
                new Slime(X + Size / 2, Y - 10, Z, newSize) { Cooldown = 120 };

            hijo1.vX = -8;
            hijo1.vY = -10;
            hijo2.vX = 8;
            hijo2.vY = -10;

            hijo1.angleX = this.angleX;
            hijo1.angleY = this.angleY;
            hijo2.angleX = this.angleX;
            hijo2.angleY = this.angleY;

            return new List<Slime> { hijo1, hijo2 };
        }

        public void Fusionar(Slime otro)
        {
            double vol1 = Math.Pow(this.Size, 3);
            double vol2 = Math.Pow(otro.Size, 3);
            this.Size = (float) Math.Pow(vol1 + vol2, 1.0 / 3.0);
            this.Cooldown = 30;

            this.vY = -8;
        }

        public void Render(Graphics g, int cX, int cY, bool isSelected)
        {
            float s = Size / 2;

            Vector3[] cubePoints =
            {
                new Vector3(-s, -s, -s),
                new Vector3(s, -s, -s),
                new Vector3(s, s, -s),
                new Vector3(-s, s, -s),
                new Vector3(-s, -s, s),
                new Vector3(s, -s, s),
                new Vector3(s, s, s),
                new Vector3(-s, s, s)
            };

            float eS = Size * 0.15f;
            float eO = Size * 0.25f;

            float eyeDepth = -s - 1;

            Vector3[] eyeL =
            {
                new Vector3(-eO - eS, -eO - eS, eyeDepth),
                new Vector3(-eO + eS, -eO - eS, eyeDepth),
                new Vector3(-eO + eS, -eO + eS, eyeDepth),
                new Vector3(-eO - eS, -eO + eS, eyeDepth)
            };
            Vector3[] eyeR =
            {
                new Vector3(eO - eS, -eO - eS, eyeDepth),
                new Vector3(eO + eS, -eO - eS, eyeDepth),
                new Vector3(eO + eS, -eO + eS, eyeDepth),
                new Vector3(eO - eS, -eO + eS, eyeDepth)
            };

            PointF[] pBody = Proyectar(cubePoints, cX, cY);

            Color renderColor =
                isSelected ? Color.FromArgb(200, 200, 255, 200) : bodyColor;
            DrawWireframe (g, pBody, renderColor);

            PointF[] pEyeL = Proyectar(eyeL, cX, cY);
            PointF[] pEyeR = Proyectar(eyeR, cX, cY);

            using (Brush b = new SolidBrush(eyeColor))
            {
                g.FillPolygon (b, pEyeL);
                g.FillPolygon (b, pEyeR);
            }
        }

        private PointF[] Proyectar(Vector3[] points, int cX, int cY)
        {
            PointF[] projected = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var v = points[i].RotateX(angleX).RotateY(angleY);
                float f = 500 / (500 + v.Z + Z);
                projected[i] =
                    new PointF(cX + (v.X + X) * f, cY + (v.Y + Y) * f);
            }
            return projected;
        }

        private void DrawWireframe(Graphics g, PointF[] p, Color col)
        {
            int[][] lines =
            {
                new int[] { 0, 1 },
                new int[] { 1, 2 },
                new int[] { 2, 3 },
                new int[] { 3, 0 },
                new int[] { 4, 5 },
                new int[] { 5, 6 },
                new int[] { 6, 7 },
                new int[] { 7, 4 },
                new int[] { 0, 4 },
                new int[] { 1, 5 },
                new int[] { 2, 6 },
                new int[] { 3, 7 }
            };
            using (Pen pen = new Pen(col, 2))
                foreach (var l in lines) g.DrawLine(pen, p[l[0]], p[l[1]]);
        }
    }

    public struct Vector3
    {
        public float

                X,
                Y,
                Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3 RotateX(float a)
        {
            float
                cos = (float) Math.Cos(a),
                sin = (float) Math.Sin(a);
            return new Vector3(X, Y * cos - Z * sin, Y * sin + Z * cos);
        }

        public Vector3 RotateY(float a)
        {
            float
                cos = (float) Math.Cos(a),
                sin = (float) Math.Sin(a);
            return new Vector3(X * cos + Z * sin, Y, -X * sin + Z * cos);
        }
    }
}
