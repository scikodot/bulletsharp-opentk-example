using System;
using System.Drawing;
using System.Security;
using BulletSharp;
using BulletSharp.Math;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Vector3 = OpenTK.Vector3;

namespace BasicDemo
{
    class ContactSensorCallback : ContactResultCallback
    {
        private RigidBody _monitoredBody;
        private object _context; // External information for contact processing

        // Constructor, pass whatever context you want to have available when processing contacts.
        // You may also want to set CollisionFilterGroups and CollisionFilterMask
        //  (supplied by the superclass) for NeedsCollision().
        public ContactSensorCallback(RigidBody monitoredBody, object context /*, ... */)
        {
            _monitoredBody = monitoredBody;
            _context = context;
        }

        // If you don't want to consider collisions where the bodies are joined by a constraint, override NeedsCollision:
        // However, if you use a CollisionObject for #body instead of a RigidBody,
        //  then this is unnecessary — CheckCollideWithOverride isn't available.
        public override bool NeedsCollision(BroadphaseProxy proxy)
        {
            // superclass will check CollisionFilterGroup and CollisionFilterMask
            if (base.NeedsCollision(proxy))
            {
                // if passed filters, may also want to avoid contacts between constraints
                //return body.CheckCollideWithOverride(proxy.ClientObject as CollisionObject);
            }

            return false;
        }

        // Called with each contact for your own processing (e.g. test if contacts fall in within sensor parameters)
        public override float AddSingleResult(ManifoldPoint contact,
            CollisionObjectWrapper colObj0, int partId0, int index0,
            CollisionObjectWrapper colObj1, int partId1, int index1)
        {
            Vector3 collisionPoint; // relative to body
            if (colObj0.CollisionObject == _monitoredBody)
            {
                var vec = contact.LocalPointA;
                collisionPoint = new Vector3(vec.X, vec.Y, vec.Z);
            }
            else
            {
                System.Diagnostics.Debug.Assert(colObj1.CollisionObject == _monitoredBody);
                var vec = contact.LocalPointA;
                collisionPoint = new Vector3(vec.X, vec.Y, vec.Z);
            }

            BasicDemo.colorCube = Color.Orange;

            // do stuff with the collision point
            return 0; // not actually sure if return value is used for anything...?
        }
    }

    class BasicDemo : GameWindow
    {
        private Physics _physics;
        private float _frameTime;
        private int _fps;
        private float aspectRatio;
        private Matrix4 perspective;
        private Matrix4 lookAt;
        private Vector3 position = new Vector3(0, 10, 30);

        public static Color colorCube = Color.Yellow;
        public static bool isHit = false;

        public BasicDemo(GraphicsMode mode)
            : base(800, 600,
            mode, "BulletSharp OpenTK Demo")
        {
            VSync = VSyncMode.Off;
            _physics = new Physics();
        }

        protected override void OnLoad(System.EventArgs e)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(Color.MidnightBlue);

            GL.Enable(EnableCap.ColorMaterial);
            GL.Enable(EnableCap.Light0);
            GL.Enable(EnableCap.Lighting);
        }

        protected override void OnUnload(System.EventArgs e)
        {
            _physics.ExitPhysics();
            base.OnUnload(e);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            _physics.Update((float)e.Time);

            var keyboard = Keyboard.GetState();
            if (keyboard[Key.Escape] || keyboard[Key.Q])
                Exit();

            var input = Keyboard.GetState();

            // camera parameters
            const float cameraSpeed = 3f;

            // panning
            if (input.IsKeyDown(Key.W))
                position += Vector3.UnitY * cameraSpeed * (float)e.Time; // Up 
            if (input.IsKeyDown(Key.S))
                position -= Vector3.UnitY * cameraSpeed * (float)e.Time; // Down
            if (input.IsKeyDown(Key.A))
                position -= Vector3.UnitX * cameraSpeed * (float)e.Time; // Left
            if (input.IsKeyDown(Key.D))
                position += Vector3.UnitX * cameraSpeed * (float)e.Time; // Right

            var mouse = Mouse.GetCursorState();
            var window = Location;
            var cursor = new Point(mouse.X - window.X - 8, mouse.Y - window.Y - 38);
            if (cursor.X >= 0 && cursor.X < Width && cursor.Y >= 0 && cursor.Y < Height)
            {
                OpenTK.Vector4 ndcStart = new OpenTK.Vector4(
                ((float)cursor.X / Width - 0.5f) * 2,
                ((float)(Height - cursor.Y) / Height - 0.5f) * 2,
                -1, 1);
                OpenTK.Vector4 ndcEnd = new OpenTK.Vector4(
                    ((float)cursor.X / Width - 0.5f) * 2,
                    ((float)(Height - cursor.Y) / Height - 0.5f) * 2,
                    0, 1);

                var projInv = Matrix4.Invert(Matrix4.Transpose(perspective));
                var viewInv = Matrix4.Invert(Matrix4.Transpose(lookAt));

                var rayStartCamera = projInv * ndcStart;
                rayStartCamera /= rayStartCamera.W;
                var rayStartWorld = viewInv * rayStartCamera;
                rayStartWorld /= rayStartWorld.W;

                var rayEndCamera = projInv * ndcEnd;
                rayEndCamera /= rayEndCamera.W;
                var rayEndWorld = viewInv * rayEndCamera;
                rayEndWorld /= rayEndWorld.W;

                var rayDir = OpenTK.Vector4.Normalize(rayEndWorld - rayStartWorld);
                rayEndWorld = rayDir * 1000;

                var source = new BulletSharp.Math.Vector3(rayStartWorld.X, rayStartWorld.Y, rayStartWorld.Z);
                var dest = new BulletSharp.Math.Vector3(rayEndWorld.X, rayEndWorld.Y, rayEndWorld.Z);
                using (var cb = new ClosestRayResultCallback(ref source, ref dest))
                {
                    _physics.World.RayTestRef(ref source, ref dest, cb);
                    if (cb.HasHit)
                    {
                        isHit = true;
                    }
                    else
                    {
                        isHit = false;
                    }
                }
            }

            Title = $"BulletSharp OpenTK Demo, MousePos = ({cursor.X}, {cursor.Y})";
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            _frameTime += (float)e.Time;
            _fps++;
            if (_frameTime >= 1)
            {
                _frameTime = 0;
                _fps = 0;
            }

            GL.Viewport(0, 0, Width, Height);

            aspectRatio = Width / (float)Height;
            perspective = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspectRatio, 0.1f, 100);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref perspective);

            lookAt = Matrix4.LookAt(position, position + new Vector3(0, 0, -1), Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            InitCubeBuffer();

            var monitoredBody = (RigidBody)_physics.World.CollisionObjectArray[0];
            object context = "context";

            _physics.World.ContactPairTest(_physics.World.CollisionObjectArray[1], _physics.World.CollisionObjectArray[0], new ContactSensorCallback(monitoredBody, context));

            foreach (RigidBody body in _physics.World.CollisionObjectArray)
            {
                Matrix4 modelLookAt = Convert(body.MotionState.WorldTransform) * lookAt;

                GL.LoadMatrix(ref modelLookAt);

                if ("Ground".Equals(body.UserObject))
                {
                    DrawCube(Color.Green, 50.0f);
                    continue;
                }

                if (isHit)
                    colorCube = Color.Magenta;
                else
                {
                    if (body.ActivationState != ActivationState.ActiveTag)
                        colorCube = Color.Red;
                }

                DrawCubeBuffer(colorCube, 1);
            }

            UninitCubeBuffer();

            SwapBuffers();
        }

        private void DrawCube(Color color)
        {
            DrawCube(color, 1.0f);
        }

        private void DrawCube(Color color, float size)
        {
            GL.Begin(PrimitiveType.Quads);

            GL.Color3(color);
            GL.Vertex3(-size, -size, -size);
            GL.Vertex3(-size, size, -size);
            GL.Vertex3(size, size, -size);
            GL.Vertex3(size, -size, -size);

            GL.Vertex3(-size, -size, -size);
            GL.Vertex3(size, -size, -size);
            GL.Vertex3(size, -size, size);
            GL.Vertex3(-size, -size, size);

            GL.Vertex3(-size, -size, -size);
            GL.Vertex3(-size, -size, size);
            GL.Vertex3(-size, size, size);
            GL.Vertex3(-size, size, -size);

            GL.Vertex3(-size, -size, size);
            GL.Vertex3(size, -size, size);
            GL.Vertex3(size, size, size);
            GL.Vertex3(-size, size, size);

            GL.Vertex3(-size, size, -size);
            GL.Vertex3(-size, size, size);
            GL.Vertex3(size, size, size);
            GL.Vertex3(size, size, -size);

            GL.Vertex3(size, -size, -size);
            GL.Vertex3(size, size, -size);
            GL.Vertex3(size, size, size);
            GL.Vertex3(size, -size, size);

            GL.End();
        }

        private readonly float[] _vertices = new float[] {
            1,1,1,  -1,1,1,  -1,-1,1,  1,-1,1,
            1,1,1,  1,-1,1,  1,-1,-1,  1,1,-1,
            1,1,1,  1,1,-1,  -1,1,-1,  -1,1,1,
            -1,1,1,  -1,1,-1,  -1,-1,-1,  -1,-1,1,
            -1,-1,-1,  1,-1,-1,  1,-1,1,  -1,-1,1,
            1,-1,-1,  -1,-1,-1,  -1,1,-1,  1,1,-1};

        private readonly float[] _normals = new float[] {
            0,0,1,  0,0,1,  0,0,1,  0,0,1,
            1,0,0,  1,0,0,  1,0,0,  1,0,0,
            0,1,0,  0,1,0,  0,1,0,  0,1,0,
            -1,0,0,  -1,0,0, -1,0,0,  -1,0,0,
            0,-1,0,  0,-1,0,  0,-1,0,  0,-1,0,
            0,0,-1,  0,0,-1,  0,0,-1,  0,0,-1};

        private readonly byte[] _indices = {
            0,1,2,3,
            4,5,6,7,
            8,9,10,11,
            12,13,14,15,
            16,17,18,19,
            20,21,22,23};

        private void InitCubeBuffer()
        {
            GL.EnableClientState(ArrayCap.NormalArray);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.NormalPointer(NormalPointerType.Float, 0, _normals);
            GL.VertexPointer(3, VertexPointerType.Float, 0, _vertices);
        }

        private void UninitCubeBuffer()
        {
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.NormalArray);
        }

        private void DrawCubeBuffer(Color color, float size)
        {
            GL.Color3(color);
            GL.DrawElements(PrimitiveType.Quads, 24, DrawElementsType.UnsignedByte, _indices);
        }

        private static Matrix4 Convert(BulletSharp.Math.Matrix m)
        {
            return new Matrix4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }
    }
}