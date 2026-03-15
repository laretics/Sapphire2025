using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO;

namespace MonoGameRenderer
{
    public class Render2DProcess : Game
    {
        private GraphicsDeviceManager mvarGraphics;
        private BasicEffect mvarEffect;
        private VertexPositionColor[] mcolVertices;
        private short[] mcolIndices;
        private float mvarAngle;
        private RenderTarget2D mvarRenderTarget;
        public volatile byte[] LastFrameBuffer;

        public Render2DProcess()
        {
            mvarGraphics= new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Define the 8 corners of the cube
            mcolVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(-1, -1, -1), Microsoft.Xna.Framework.Color.Red),
                new VertexPositionColor(new Vector3(-1,  1, -1), Microsoft.Xna.Framework.Color.Green),
                new VertexPositionColor(new Vector3( 1,  1, -1), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3( 1, -1, -1), Microsoft.Xna.Framework.Color.Yellow),
                new VertexPositionColor(new Vector3(-1, -1,  1), Microsoft.Xna.Framework.Color.Cyan),
                new VertexPositionColor(new Vector3(-1,  1,  1), Microsoft.Xna.Framework.Color.Magenta),
                new VertexPositionColor(new Vector3( 1,  1,  1), Microsoft.Xna.Framework.Color.White),
                new VertexPositionColor(new Vector3( 1, -1,  1), Microsoft.Xna.Framework.Color.Black),
            };

            // Define the indices for the 12 triangles (2 per face)
            mcolIndices = new short[]
            {
                0, 1, 2, 0, 2, 3, // back face
                4, 6, 5, 4, 7, 6, // front face
                4, 5, 1, 4, 1, 0, // left face
                3, 2, 6, 3, 6, 7, // right face
                1, 5, 6, 1, 6, 2, // top face
                4, 0, 3, 4, 3, 7  // bottom face
            };

            mvarEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };

        }

        protected override void LoadContent()
        {
            mvarRenderTarget = new RenderTarget2D(
                GraphicsDevice,
                GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                DepthFormat.Depth24);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            mvarAngle += (float)gameTime.ElapsedGameTime.TotalSeconds;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            // Renderiza al RenderTarget2D
            GraphicsDevice.SetRenderTarget(mvarRenderTarget);
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            // Dibuja el cubo como antes
            var view = Matrix.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.Up);
            var projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4,
                GraphicsDevice.Viewport.AspectRatio,
                0.1f,
                100f);

            mvarEffect.View = view;
            mvarEffect.Projection = projection;
            mvarEffect.World = Matrix.CreateRotationY(mvarAngle) * Matrix.CreateRotationX(mvarAngle / 2);

            foreach (var pass in mvarEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    mcolVertices, 0, mcolVertices.Length,
                    mcolIndices, 0, mcolIndices.Length / 3);
            }

            // Vuelve a renderizar a la pantalla principal
            GraphicsDevice.SetRenderTarget(null);

            // Dibuja el contenido del render target en pantalla
            using (var spriteBatch = new SpriteBatch(GraphicsDevice))
            {
                spriteBatch.Begin();
                spriteBatch.Draw(mvarRenderTarget, Vector2.Zero, Microsoft.Xna.Framework.Color.White);
                spriteBatch.End();
            }

            LastFrameBuffer = GetCurrentFrameAsJpeg(); //Entregamos el render al servidor MJPEG.

            base.Draw(gameTime);
        }

        public byte[] GetCurrentFrameAsJpeg()
        {
            int width = mvarRenderTarget.Width;
            int height = mvarRenderTarget.Height;
            Microsoft.Xna.Framework.Color[] data = new Microsoft.Xna.Framework.Color[width * height];
            mvarRenderTarget.GetData(data);

            using (var image = new Image<Rgba32>(width, height))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Microsoft.Xna.Framework.Color c = data[y * width + x];
                        image[x, y] = new Rgba32(c.R, c.G, c.B, c.A);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    image.Save(ms, new JpegEncoder { Quality = 80 });
                    return ms.ToArray();
                }
            }
        }
    }
}