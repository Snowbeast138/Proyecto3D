using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using Timer = System.Windows.Forms.Timer;

namespace Proyecto3D
{
    public partial class Form1 : Form
    {
        private Timer gameTimer;

        private List<Slime> slimes;

        private List<Particle> particles;

        private Point lastMousePos;

        private Slime draggedSlime = null;

        private bool mouseMoved = false;

        public Form1()
        {
            this.DoubleBuffered = true;
            this.Size = new Size(800, 600);
            this.Text = "Proyecto 3D-Slime";
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.KeyPreview = true;

            slimes = new List<Slime> { new Slime(0, -100, 300, 100) };
            particles = new List<Particle>();

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
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                if (particles[i].Update()) particles.RemoveAt(i);
            }

            for (int i = 0; i < slimes.Count; i++)
            {
                bool isBeingDragged = (slimes[i] == draggedSlime);
                slimes[i].UpdatePhysics(isBeingDragged, particles);

                for (int j = i + 1; j < slimes.Count; j++)
                {
                    var s1 = slimes[i];
                    var s2 = slimes[j];

                    float dist = s1.GetDistancia(s2);
                    float distanciaColision = (s1.Size + s2.Size) * 0.6f;

                    if (dist > 5) s1.AtraerA(s2);

                    if (
                        dist < distanciaColision &&
                        s1.Cooldown <= 0 &&
                        s2.Cooldown <= 0
                    )
                    {
                        s1.Fusionar (s2);
                        slimes.RemoveAt (j);
                        if (draggedSlime == s2) draggedSlime = s1;
                        break;
                    }
                }
            }
            this.Invalidate();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                foreach (var s in slimes) s.Size *= 1.1f;
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                foreach (var s in slimes) if (s.Size > 5) s.Size *= 0.9f;
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int
                    cX = this.Width / 2,
                    cY = this.Height / 2;
                draggedSlime = null;
                mouseMoved = false;
                lastMousePos = e.Location;

                foreach (var s in slimes.OrderBy(s => s.Z))
                {
                    float f = 500 / (500 + s.Z);
                    float
                        pX = cX + s.X * f,
                        pY = cY + s.Y * f;
                    if (
                        Math
                            .Sqrt(Math.Pow(pX - e.X, 2) +
                            Math.Pow(pY - e.Y, 2)) <
                        s.Size * f
                    )
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
                float
                    dx = e.X - lastMousePos.X,
                    dy = e.Y - lastMousePos.Y;
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
                if (draggedSlime.Size > 25)
                {
                    var hijos = draggedSlime.Dividir(particles);
                    slimes.Remove (draggedSlime);
                    slimes.AddRange (hijos);
                }
            }
            draggedSlime = null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int
                cX = this.Width / 2,
                cY = this.Height / 2;

            foreach (var slime in slimes.OrderByDescending(s => s.Z))
            slime.Render(e.Graphics, cX, cY, slime == draggedSlime);

            foreach (var p in particles.OrderByDescending(p => p.Z))
            p.Render(e.Graphics, cX, cY);
        }
    }

    public static class AudioEngine
    {
        public static void PlayJump() =>
            Task
                .Run(() =>
                {
                    try
                    {
                        Console.Beep(300, 80);
                    }
                    catch
                    {
                    }
                });

        public static void PlayDivide() =>
            Task
                .Run(() =>
                {
                    try
                    {
                        Console.Beep(600, 100);
                        Console.Beep(800, 150);
                    }
                    catch
                    {
                    }
                });
    }

    public class Particle
    {
        public float

                X,
                Y,
                Z,
                vX,
                vY,
                vZ,
                Size;

        public int

                Life,
                MaxLife;

        public Particle(
            float x,
            float y,
            float z,
            float size,
            float vx,
            float vy,
            float vz
        )
        {
            X = x;
            Y = y;
            Z = z;
            Size = size;
            vX = vx;
            vY = vy;
            vZ = vz;
            MaxLife = new Random().Next(20, 40);
            Life = MaxLife;
        }

        public bool Update()
        {
            vY += 0.5f;
            X += vX;
            Y += vY;
            Z += vZ;
            Life--;
            return Life <= 0;
        }

        public void Render(Graphics g, int cX, int cY)
        {
            float f = 500 / (500 + Z);
            float pSize = Size * f * ((float) Life / MaxLife);
            using (Brush b = new SolidBrush(Color.FromArgb(200, 50, 200, 50)))
                g
                    .FillRectangle(b,
                    cX + X * f - pSize / 2,
                    cY + Y * f - pSize / 2,
                    pSize,
                    pSize);
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

        private float targetAngleY = 0;

        public float

                vX = 0,
                vY = 0,
                vZ = 0;

        // --- VARIABLES DE GELATINA (SQUASH & STRETCH) ---
        public float scaleY = 1.0f; // Escala vertical actual

        public float vScaleY = 0.0f; // Velocidad de deformación

        private int jumpTimer = 0;

        private static Random rand = new Random();

        private Color outerColor = Color.FromArgb(120, 100, 255, 100);

        private Color coreColor = Color.FromArgb(255, 40, 180, 40);

        private Color faceColor = Color.FromArgb(255, 230, 255, 230);

        public Slime(float x, float y, float z, float size)
        {
            X = x;
            Y = y;
            Z = z;
            Size = size;
            jumpTimer = rand.Next(30, 100);
        }

        public void UpdatePhysics(bool isBeingDragged, List<Particle> particles)
        {
            if (Cooldown > 0) Cooldown--;

            // --- MOTOR DE RESORTE (FÍSICA DE GELATINA) ---
            // Intenta volver a su escala normal (1.0) usando la Ley de Hooke
            float springForce = (1.0f - scaleY) * 0.15f;
            vScaleY += springForce;
            vScaleY *= 0.85f; // Fricción del resorte para que no rebote para siempre
            scaleY += vScaleY;

            // Límite para evitar que se invierta matemáticamente
            if (scaleY < 0.2f) scaleY = 0.2f;

            if (isBeingDragged)
            {
                vX = vY = vZ = 0;
                targetAngleY = angleY;
                return;
            }

            if (Math.Abs(vX) > 0.5f || Math.Abs(vZ) > 0.5f)
            {
                targetAngleY = (float) Math.Atan2(-vX, -vZ);
            }

            angleX += (0 - angleX) * 0.08f;

            float diff = targetAngleY - angleY;
            while (diff <= -Math.PI) diff += (float)(2 * Math.PI);
            while (diff > Math.PI) diff -= (float)(2 * Math.PI);
            angleY += diff * 0.1f;

            vY += 0.6f;
            X += vX;
            Y += vY;
            Z += vZ;
            vX *= 0.92f;
            vZ *= 0.92f;

            float
                limX = 350,
                minZ = 150,
                maxZ = 650,
                bounce = -0.7f;

            if (X > limX)
            {
                X = limX;
                vX *= bounce;
                EmitirParticulasPared(particles, -5, 0);
                vScaleY += 0.2f;
            } // Tiembla al chocar
            if (X < -limX)
            {
                X = -limX;
                vX *= bounce;
                EmitirParticulasPared(particles, 5, 0);
                vScaleY += 0.2f;
            }
            if (Z > maxZ)
            {
                Z = maxZ;
                vZ *= bounce;
                EmitirParticulasPared(particles, 0, -5);
                vScaleY += 0.2f;
            }
            if (Z < minZ)
            {
                Z = minZ;
                vZ *= bounce;
                EmitirParticulasPared(particles, 0, 5);
                vScaleY += 0.2f;
            }

            float limY = 200 - (Size / 2);
            if (Y >= limY)
            {
                // APLASTAMIENTO AL CAER
                if (vY > 2.0f)
                {
                    vScaleY -= vY * 0.03f; // Se aplasta dependiendo de qué tan duro cayó
                }

                Y = limY;
                vY = 0;
                jumpTimer--;
                if (jumpTimer <= 0)
                {
                    jumpTimer = rand.Next(40, 120);
                    vY = -rand.Next(10, 16);
                    vX += rand.Next(-8, 9);
                    vZ += rand.Next(-8, 9);

                    // ESTIRAMIENTO AL SALTAR
                    vScaleY += 0.35f;

                    AudioEngine.PlayJump();

                    for (int i = 0; i < 8; i++)
                    particles
                        .Add(new Particle(X +
                            rand.Next((int) - Size / 2, (int) Size / 2),
                            Y + Size / 2,
                            Z,
                            Size * 0.15f,
                            rand.Next(-3, 4),
                            rand.Next(-5, 0),
                            rand.Next(-3, 4)));
                }
            }
        }

        private void EmitirParticulasPared(
            List<Particle> particles,
            float pushX,
            float pushZ
        )
        {
            for (int i = 0; i < 6; i++)
            particles
                .Add(new Particle(X,
                    Y,
                    Z,
                    Size * 0.15f,
                    pushX + rand.Next(-3, 4),
                    rand.Next(-4, 1),
                    pushZ + rand.Next(-3, 4)));
        }

        public void Rotate(float ax, float ay)
        {
            angleY += ax;
            angleX += ay;
            targetAngleY = angleY;
        }

        public void AtraerA(Slime otro)
        {
            if (Cooldown <= 0)
            {
                vX += (otro.X - X) * 0.03f;
                vZ += (otro.Z - Z) * 0.03f;
            }
        }

        public float GetDistancia(Slime otro) =>
            (float)
            Math
                .Sqrt(Math.Pow(X - otro.X, 2) +
                Math.Pow(Y - otro.Y, 2) +
                Math.Pow(Z - otro.Z, 2));

        public List<Slime> Dividir(List<Particle> particles)
        {
            float newSize = Size / 1.25992f;
            var h1 =
                new Slime(X - Size / 2,
                    Y - 10,
                    Z,
                    newSize)
                { Cooldown = 120, vX = -8, vY = -10, vScaleY = 0.4f };
            var h2 =
                new Slime(X + Size / 2,
                    Y - 10,
                    Z,
                    newSize)
                { Cooldown = 120, vX = 8, vY = -10, vScaleY = 0.4f };
            h1.angleX = h2.angleX = angleX;
            h1.angleY = h2.angleY = angleY;

            AudioEngine.PlayDivide();

            for (int i = 0; i < 20; i++)
            particles
                .Add(new Particle(X,
                    Y,
                    Z,
                    Size * 0.2f,
                    rand.Next(-6, 7),
                    rand.Next(-8, 2),
                    rand.Next(-6, 7)));

            return new List<Slime> { h1, h2 };
        }

        public void Fusionar(Slime otro)
        {
            this.Size =
                (float)
                Math
                    .Pow(Math.Pow(this.Size, 3) + Math.Pow(otro.Size, 3),
                    1.0 / 3.0);
            this.Cooldown = 30;
            this.vY = -8;
            this.vScaleY = 0.5f; // Estirón dramático al fusionarse
        }

        public void Render(Graphics g, int cX, int cY, bool isSelected)
        {
            float s = Size / 2;
            float zF = -s - 0.5f;

            Vector3[] outer = GetCubePoints(s);
            Vector3[] core = GetCubePoints(s * 0.6f);

            float
                eS = Size * 0.12f,
                eO = Size * 0.22f;
            float
                mW = Size * 0.15f,
                mH = Size * 0.08f,
                mY = Size * 0.15f;

            Vector3[] eyeL =
                GetRect(-eO - eS, -eO + eS, -eO - eS, -eO + eS, zF);
            Vector3[] eyeR = GetRect(eO - eS, eO + eS, -eO - eS, -eO + eS, zF);
            Vector3[] mouth = GetRect(-mW, mW, mY, mY + mH, zF);

            // APLICAMOS LA DEFORMACIÓN DE GELATINA (Squash & Stretch)
            outer = ApplySquash(outer, s);
            core = ApplySquash(core, s);
            eyeL = ApplySquash(eyeL, s);
            eyeR = ApplySquash(eyeR, s);
            mouth = ApplySquash(mouth, s);

            Color renderColor =
                isSelected ? Color.FromArgb(200, 200, 255, 200) : outerColor;

            DrawWireframe(g, Proyectar(core, cX, cY), coreColor, 4);
            DrawWireframe(g, Proyectar(outer, cX, cY), renderColor, 2);

            using (Brush b = new SolidBrush(faceColor))
            {
                g.FillPolygon(b, Proyectar(eyeL, cX, cY));
                g.FillPolygon(b, Proyectar(eyeR, cX, cY));
                g.FillPolygon(b, Proyectar(mouth, cX, cY));
            }
        }

        // --- SISTEMA DE DEFORMACIÓN MATEMÁTICA ---
        private Vector3[] ApplySquash(Vector3[] pts, float baseS)
        {
            // Conservación del volumen: si se achica en Y, crece en X y Z
            float scaleXZ = 1.0f / (float) Math.Sqrt(scaleY);

            // Compensación para que el cubo siempre toque el suelo por la base
            float yOffset = baseS * (1.0f - scaleY);

            for (int i = 0; i < pts.Length; i++)
            {
                pts[i].X *= scaleXZ;
                pts[i].Z *= scaleXZ;
                pts[i].Y = (pts[i].Y * scaleY) + yOffset;
            }
            return pts;
        }

        private Vector3[] GetCubePoints(float s) =>
            new Vector3[] {
                new Vector3(-s, -s, -s),
                new Vector3(s, -s, -s),
                new Vector3(s, s, -s),
                new Vector3(-s, s, -s),
                new Vector3(-s, -s, s),
                new Vector3(s, -s, s),
                new Vector3(s, s, s),
                new Vector3(-s, s, s)
            };

        private Vector3[]
        GetRect(float x1, float x2, float y1, float y2, float z) =>
            new Vector3[] {
                new Vector3(x1, y1, z),
                new Vector3(x2, y1, z),
                new Vector3(x2, y2, z),
                new Vector3(x1, y2, z)
            };

        private PointF[] Proyectar(Vector3[] points, int cX, int cY)
        {
            PointF[] p = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var v = points[i].RotateX(angleX).RotateY(angleY);
                float f = 500 / (500 + v.Z + Z);
                p[i] = new PointF(cX + (v.X + X) * f, cY + (v.Y + Y) * f);
            }
            return p;
        }

        private void DrawWireframe(
            Graphics g,
            PointF[] p,
            Color col,
            float thickness
        )
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
            using (Pen pen = new Pen(col, thickness))
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

        public Vector3 RotateX(float a) =>
            new Vector3(X,
                Y * (float) Math.Cos(a) - Z * (float) Math.Sin(a),
                Y * (float) Math.Sin(a) + Z * (float) Math.Cos(a));

        public Vector3 RotateY(float a) =>
            new Vector3(X * (float) Math.Cos(a) + Z * (float) Math.Sin(a),
                Y,
                -X * (float) Math.Sin(a) + Z * (float) Math.Cos(a));
    }
}
