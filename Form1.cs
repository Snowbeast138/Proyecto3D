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
            // Main update loop (runs every frame via a Timer).
            // Responsibilities:
            // - Update particles and remove expired ones
            // - Update each slime: handle forced division, physics, and merging
            // - Trigger rendering by invalidating the form
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                if (particles[i].Update()) particles.RemoveAt(i);
            }

            for (int i = 0; i < slimes.Count; i++)
            {
                // --- 1. CHEQUEO DE DIVISIÓN POR TENSIÓN (DERRETIMIENTO MÁXIMO) ---
                if (slimes[i].NeedsDivision)
                {
                    if (slimes[i].Size > 25)
                    {
                        var hijos = slimes[i].Dividir(particles);
                        if (draggedSlime == slimes[i]) draggedSlime = null; // Lo soltamos automáticamente
                        slimes.RemoveAt (i);
                        slimes.AddRange (hijos);
                        i--;
                        continue;
                    }
                    else
                    {
                        slimes[i].NeedsDivision = false; // Es muy pequeño para dividirse
                    }
                }

                // --- 2. ACTUALIZACIÓN FÍSICA NORMAL ---
                bool isBeingDragged = (slimes[i] == draggedSlime);
                slimes[i].UpdatePhysics(isBeingDragged, particles);

                // --- 3. LÓGICA DE FUSIÓN ---
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
            // Simple keyboard controls to scale all slimes up/down.
            // Uses '+' and '-' keys (both numeric pad and main keys).
            if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
                foreach (var s in slimes) s.Size *= 1.1f;
            else if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
                foreach (var s in slimes) if (s.Size > 5) s.Size *= 0.9f;
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            // On left mouse down: attempt to pick (select) a slime for dragging.
            // The slimes are tested in order of increasing depth (Z) so that
            // nearer slimes can be selected before farther ones.
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
            // While dragging a slime, update its position, rotation and
            // impart 'bend' inertia for the liquid-like deformation.
            if (draggedSlime != null)
            {
                float
                    dx = e.X - lastMousePos.X,
                    dy = e.Y - lastMousePos.Y;
                if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2) mouseMoved = true;

                draggedSlime.Rotate(dx * 0.015f, dy * 0.015f);
                draggedSlime.X += dx;
                draggedSlime.Y += dy;

                // INYECTAMOS INERCIA PARA EL EFECTO LÍQUIDO
                draggedSlime.bendX += dx;
                draggedSlime.bendY += dy;

                lastMousePos = e.Location;
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            // On mouse release: if the slime was clicked (not moved) and
            // large enough, split it into two child slimes.
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
            // Renders slimes and particles. Slimes are drawn from far to
            // near to ensure correct overlap; particles are rendered after.
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
        // Small helper to run simple beep sounds asynchronously so audio
        // does not block the UI thread. Uses Console.Beep for simplicity.
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
        // Simple particle used for splash and breakup effects.
        // Particles have position, velocity, size and a limited life.
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
            // Update particle physics: gravity and position.
            // Returns true when particle life has ended and it should
            // be removed from the particles list.
            vY += 0.5f;
            X += vX;
            Y += vY;
            Z += vZ;
            Life--;
            return Life <= 0;
        }

        public void Render(Graphics g, int cX, int cY)
        {
            // Render the particle as a small rectangle with depth scaling
            // and fade-out based on remaining life.
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
        // Slime represents a soft body made from an 8-point cube mesh.
        // It stores position, velocity, orientation and soft-body state
        // such as scaleY (squash/stretch), dragMelt (tension for splitting)
        // and bendX/bendY (inertia for deformation). Slimes can split,
        // merge, jump and emit particles when hitting walls or jumping.
        public float

                X,
                Y,
                Z,
                Size;

        public int Cooldown = 0;

        public bool NeedsDivision = false; // Bandera para alertar al GameLoop

        private float

                angleX = 0,
                angleY = 0;

        private float targetAngleY = 0;

        public float

                vX = 0,
                vY = 0,
                vZ = 0;

        // --- VARIABLES DE GELATINA Y DERRETIMIENTO ---
        public float scaleY = 1.0f;

        public float vScaleY = 0.0f;

        public float dragMelt = 0.0f;

        // --- INERCIA (Dirección de la curvatura) ---
        public float bendX = 0.0f;

        public float bendY = 0.0f;

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
            // Core per-frame physics for a single slime.
            // - Handles dragging behavior (tension build-up and small droplets)
            // - Spring-like squash/stretch while on the ground
            // - Gravity and simple bounds with particle emission on collision
            // - Random jumping behavior and its particle burst
            if (Cooldown > 0) Cooldown--;

            // Fricción a la inercia para que la gota regrese al centro al detener el mouse
            bendX *= 0.85f;
            bendY *= 0.85f;

            // Limitamos la inercia para que no se deforme hasta el infinito si agitas muy rápido
            bendX = Math.Max(-60, Math.Min(60, bendX));
            bendY = Math.Max(-60, Math.Min(60, bendY));

            if (isBeingDragged)
            {
                vX = vY = vZ = 0;
                targetAngleY = angleY;
                scaleY += (1.0f - scaleY) * 0.1f;
                vScaleY = 0;

                // Aumenta el nivel de tensión (derretimiento)
                dragMelt += 0.015f;

                // Si la masa no aguanta más, se rompe por tensión
                if (dragMelt >= 1.0f)
                {
                    dragMelt = 1.0f;
                    NeedsDivision = true;
                }

                // Gotitas de gelatina
                if (rand.Next(100) < 15)
                {
                    particles
                        .Add(new Particle(X +
                            rand.Next((int) - Size / 4, (int) Size / 4),
                            Y + Size / 2 + (dragMelt * Size * 1.5f),
                            Z,
                            Size * 0.1f,
                            0,
                            rand.Next(1, 4),
                            0));
                }
                return;
            }

            // --- SI LO SUELTAS ANTES DE QUE SE ROMPA ---
            NeedsDivision = false;
            if (dragMelt > 0)
            {
                dragMelt -= 0.1f;
                if (dragMelt <= 0)
                {
                    dragMelt = 0;
                    vScaleY = -0.3f; // Rebote tipo resorte
                }
            }

            // Físicas normales de salto
            float springForce = (1.0f - scaleY) * 0.15f;
            vScaleY += springForce;
            vScaleY *= 0.85f;
            scaleY += vScaleY;
            if (scaleY < 0.2f) scaleY = 0.2f;

            if (Math.Abs(vX) > 0.5f || Math.Abs(vZ) > 0.5f)
                targetAngleY = (float) Math.Atan2(-vX, -vZ);

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
            }
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
                if (vY > 2.0f) vScaleY -= vY * 0.03f;
                Y = limY;
                vY = 0;
                jumpTimer--;
                if (jumpTimer <= 0)
                {
                    jumpTimer = rand.Next(40, 120);
                    vY = -rand.Next(10, 16);
                    vX += rand.Next(-8, 9);
                    vZ += rand.Next(-8, 9);
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
            // Emit several particles when the slime hits a world boundary.
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
            // Apply a small rotation to the slime mesh (used during drag).
            angleY += ax;
            angleX += ay;
            targetAngleY = angleY;
        }

        public void AtraerA(Slime otro)
        {
            // Small attraction force used to make nearby slimes move toward
            // each other (helps merging behavior). Disabled while on cooldown.
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
            // Split this slime into two smaller slimes.
            // Produces particles to simulate breakup and returns the two
            // new Slime instances. The children inherit orientation and
            // receive initial velocities.
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
            // Merge another slime into this one by computing a new volume-
            // preserving size (cube-root of sum of volumes), set a small
            // cooldown and give a bounce impulse.
            this.Size =
                (float)
                Math
                    .Pow(Math.Pow(this.Size, 3) + Math.Pow(otro.Size, 3),
                    1.0 / 3.0);
            this.Cooldown = 30;
            this.vY = -8;
            this.vScaleY = 0.5f;
        }

        public void Render(Graphics g, int cX, int cY, bool isSelected)
        {
            // Render the slime by building outer and core cube meshes,
            // applying squash/deformation, projecting to 2D and drawing
            // a wireframe plus simple face features (eyes/mouth).
            float s = Size / 2;
            float zF = -s - 0.5f;

            Vector3[]
                outer = GetCubePoints(s),
                core = GetCubePoints(s * 0.6f);
            float
                eS = Size * 0.12f,
                eO = Size * 0.22f,
                mW = Size * 0.15f,
                mH = Size * 0.08f,
                mY = Size * 0.15f;

            Vector3[] eyeL =
                GetRect(-eO - eS, -eO + eS, -eO - eS, -eO + eS, zF);
            Vector3[] eyeR = GetRect(eO - eS, eO + eS, -eO - eS, -eO + eS, zF);
            Vector3[] mouth = GetRect(-mW, mW, mY, mY + mH, zF);

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

        private Vector3[] ApplySquash(Vector3[] pts, float baseS)
        {
            // Apply vertical squash/stretch and additional deformation
            // caused by dragMelt (tension) and bend inertia. This is the
            // heart of the soft-body visual effect; it modifies the cube
            // sample points before projection.
            float scaleXZ = 1.0f / (float) Math.Sqrt(scaleY);
            float yOffset = baseS * (1.0f - scaleY);

            for (int i = 0; i < pts.Length; i++)
            {
                float origY = pts[i].Y;

                pts[i].X *= scaleXZ;
                pts[i].Z *= scaleXZ;
                pts[i].Y = (origY * scaleY) + yOffset;

                // --- DEFORMACIÓN Y CURVATURA (EFECTO GOTA Y LÍQUIDO) ---
                if (dragMelt > 0)
                {
                    float normalizedY = (origY + baseS) / (2 * baseS);
                    if (normalizedY < 0) normalizedY = 0;
                    if (normalizedY > 1) normalizedY = 1;

                    // 1. Efecto colgar (Gravedad Hacia abajo)
                    float sag =
                        (normalizedY * normalizedY) * dragMelt * baseS * 2.5f;
                    pts[i].Y += sag;

                    // 2. Pellizco arriba y ensanchamiento abajo
                    float pinch = 1.0f - (dragMelt * 0.45f);
                    float bulge = 1.0f + (dragMelt * 0.15f);
                    float currentWidth = pinch + (bulge - pinch) * normalizedY;
                    pts[i].X *= currentWidth;
                    pts[i].Z *= currentWidth;

                    // 3. Efecto de INERCIA (Resistencia al movimiento)
                    // La parte de abajo (normalizedY = 1) se queda "rezagada" en dirección opuesta a bendX y bendY
                    pts[i].X -= bendX * normalizedY * dragMelt * 0.3f;
                    pts[i].Y -= bendY * normalizedY * dragMelt * 0.3f;
                }
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
            // Project 3D points into 2D screen coordinates with a
            // simple perspective factor (500 / (500 + Z)). Also applies
            // local rotation based on angleX/angleY.
            PointF[] p = new PointF[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var v = points[i].RotateX(angleX).RotateY(angleY);
                p[i] =
                    new PointF(cX + (v.X + X) * (500 / (500 + v.Z + Z)),
                        cY + (v.Y + Y) * (500 / (500 + v.Z + Z)));
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
            // Draws the cube edges from an index list. Used by Render to
            // visualize the outer and core wireframes.
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
        // Minimal 3D vector used for mesh point storage and simple
        // rotation helpers around X and Y axes. The rotations are used
        // when projecting the cube-based slimes.
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
