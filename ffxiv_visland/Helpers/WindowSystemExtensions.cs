using Dalamud.Interface.Windowing;
using System;
using System.Linq;

namespace visland.Helpers;

public static class WindowSystemExtensions {
    extension(WindowSystem ws) {
        public void Add(params Window[] windows) {
            foreach (var w in windows) {
                ws.AddWindow(w);
            }
        }
        public T? Get<T>() where T : Window => ws.Windows.OfType<T>().FirstOrDefault();
        public void Dispose() {
            foreach (var w in ws.Windows) {
                if (w is IDisposable d) {
                    d.Dispose();
                }
            }
            ws.RemoveAllWindows();
        }
    }
}
