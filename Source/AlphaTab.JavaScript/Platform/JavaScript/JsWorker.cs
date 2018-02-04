using System;
using AlphaTab.Haxe;
using AlphaTab.Haxe.Js;
using AlphaTab.Haxe.Js.Html;
using AlphaTab.Model;
using AlphaTab.Rendering;
using AlphaTab.Util;
using Phase;

namespace AlphaTab.Platform.JavaScript
{
    public class JsWorker
    {
        private ScoreRenderer _renderer;
        private readonly DedicatedWorkerGlobalScope _main;

        public JsWorker(DedicatedWorkerGlobalScope main)
        {
            _main = main;
            _main.AddEventListener("message", (Action<Event>)HandleMessage, false);
        }

        public static void Init()
        {
            new JsWorker(Lib.Global);
        }


        private void HandleMessage(Event e)
        {
            var data = ((MessageEvent)e).Data;
            var cmd = data ? data.cmd : "";
            switch (cmd)
            {
                case "alphaTab.initialize":
                    var settings = Settings.FromJson(data.settings, null);
                    _renderer = new ScoreRenderer(settings);
                    _renderer.PartialRenderFinished += result => _main.PostMessage(new { cmd = "alphaTab.partialRenderFinished", result = result });
                    _renderer.RenderFinished += result => _main.PostMessage(new { cmd = "alphaTab.renderFinished", result = result });
                    _renderer.PostRenderFinished += () => _main.PostMessage(new { cmd = "alphaTab.postRenderFinished", boundsLookup = _renderer.BoundsLookup.ToJson() });
                    _renderer.PreRender += result => _main.PostMessage(new { cmd = "alphaTab.preRender", result = result });
                    _renderer.Error += Error;
                    break;
                case "alphaTab.invalidate":
                    _renderer.Invalidate();
                    break;
                case "alphaTab.resize":
                    _renderer.Resize(data.width);
                    break;
                case "alphaTab.render":
                    var converter = new JsonConverter();
                    var score = converter.JsObjectToScore(data.score);
                    RenderMultiple(score, data.trackIndexes);
                    break;
                case "alphaTab.updateSettings":
                    UpdateSettings(data.settings);
                    break;
            }
        }

        private void UpdateSettings(object settings)
        {
            _renderer.UpdateSettings(Settings.FromJson(settings, null));
        }

        private void RenderMultiple(Score score, int[] trackIndexes)
        {
            try
            {
                _renderer.Render(score, trackIndexes);
            }
            catch (Exception e)
            {
                Error("render", e);
            }
        }

        private void Error(string type, Exception e)
        {
            Logger.Error(type, "An unexpected error occurred in worker", e);

            dynamic error = Json.Parse(Json.Stringify(e));

            dynamic e2 = e;

            if (e2.message)
            {
                error.message = e2.message;
            }
            if (e2.stack)
            {
                error.stack = e2.stack;
            }
            if (e2.constructor && e2.constructor.name)
            {
                error.type = e2.constructor.name;
            }
            _main.PostMessage(new { cmd = "alphaTab.error", error = new { type = type, detail = error } });
        }
    }
}