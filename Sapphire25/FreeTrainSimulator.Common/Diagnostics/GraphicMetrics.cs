using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Common.Diagnostics
{
    public sealed class GraphicMetrics : DetailInfoBase
    {
        public GraphicsMetrics CurrentMetrics { get; set; }

        public GraphicMetrics() : base(true)
        {
            this["GPU Metrics"] = null;
            this[".0"] = null;
            this["Clear Calls"] = null;
            this["Draw Calls"] = null;
            this["Primitives"] = null;
            this["Textures"] = null;
            this["Sprites"] = null;
            this["Targets"] = null;
            this["PixelShaders"] = null;
            this["VertexShaders"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["Clear Calls"] = $"{CurrentMetrics.ClearCount:N0}";
                this["Draw Calls"] = $"{CurrentMetrics.DrawCount:N0}";
                this["Primitives"] = $"{CurrentMetrics.PrimitiveCount:N0}";
                this["Textures"] = $"{CurrentMetrics.TextureCount:N0}";
                this["Sprites"] = $"{CurrentMetrics.SpriteCount:N0}";
                this["Targets"] = $"{CurrentMetrics.TargetCount:N0}";
                this["PixelShaders"] = $"{CurrentMetrics.PixelShaderCount:N0}";
                this["VertexShaders"] = $"{CurrentMetrics.VertexShaderCount:N0}";
                base.Update(gameTime);
            }
        }
    }
}
