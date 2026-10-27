using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

public static class Program {
  static bool IsHotMagenta(byte r, byte g, byte b, byte a) {
    if (a < 20) return true;
    if (g > 100) return false;
    if (r < 120) return false;
    if (b < 60) return false;
    if ((r - g) < 50) return false;
    if ((b - g) < 25) return false;
    return true;
  }
  static bool IsEdgeVoid(byte r, byte g, byte b, byte a) {
    if (IsHotMagenta(r,g,b,a)) return true;
    int max = Math.Max(r, Math.Max(g, b));
    int min = Math.Min(r, Math.Min(g, b));
    return max <= 40 && (max - min) <= 18;
  }
  static bool IsFringe(byte r, byte g, byte b, byte a) {
    if (a < 20) return true;
    if (g > 120) return false;
    if (r < 90) return false;
    return (r - g) > 35 && b > 45 && (b - g) > 15;
  }
  static int Idx(int x, int y, int w) { return y * w + x; }

  public static void Main(string[] args) {
    string src = args[0], dstPng = args[1], dstBytes = args[2];
    int size = 1024;
    float overscan = 1.45f;
    using (Bitmap bmp = new Bitmap(src)) {
      int w = bmp.Width, h = bmp.Height;
      BitmapData data = bmp.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      int stride = Math.Abs(data.Stride);
      byte[] srcPx = new byte[stride * h];
      Marshal.Copy(data.Scan0, srcPx, 0, srcPx.Length);
      bmp.UnlockBits(data);

      byte[] a = new byte[w*h], r = new byte[w*h], g = new byte[w*h], b = new byte[w*h];
      for (int y=0;y<h;y++) for (int x=0;x<w;x++) {
        int o = y*stride + x*4, i = Idx(x,y,w);
        b[i]=srcPx[o]; g[i]=srcPx[o+1]; r[i]=srcPx[o+2]; a[i]=srcPx[o+3];
      }

      bool[] visit = new bool[w*h];
      Queue<int> q = new Queue<int>();
      Action<int,int> tryEnq = delegate(int x, int y) {
        if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
        int i = Idx(x,y,w);
        if (visit[i]) return;
        if (!IsEdgeVoid(r[i], g[i], b[i], a[i])) return;
        visit[i]=true; q.Enqueue(i);
      };
      for (int x=0;x<w;x++){ tryEnq(x,0); tryEnq(x,h-1); }
      for (int y=0;y<h;y++){ tryEnq(0,y); tryEnq(w-1,y); }
      while (q.Count>0) {
        int i=q.Dequeue();
        a[i]=0; r[i]=0; g[i]=0; b[i]=0;
        int x=i%w, yy=i/w;
        tryEnq(x+1,yy); tryEnq(x-1,yy); tryEnq(x,yy+1); tryEnq(x,yy-1);
      }
      for (int i=0;i<a.Length;i++)
        if (IsHotMagenta(r[i],g[i],b[i],a[i])) { a[i]=0; r[i]=0; g[i]=0; b[i]=0; }

      for (int pass=0; pass<3; pass++) {
        byte[] ca=(byte[])a.Clone(), cr=(byte[])r.Clone(), cg=(byte[])g.Clone(), cb=(byte[])b.Clone();
        for (int y=1;y<h-1;y++) for (int x=1;x<w-1;x++) {
          int i=Idx(x,y,w);
          if (ca[i]<20) continue;
          if (!IsFringe(cr[i],cg[i],cb[i],ca[i])) continue;
          bool n = ca[Idx(x-1,y,w)]<20||ca[Idx(x+1,y,w)]<20||ca[Idx(x,y-1,w)]<20||ca[Idx(x,y+1,w)]<20;
          if (n){ a[i]=0; r[i]=0; g[i]=0; b[i]=0; }
        }
      }

      int minX=w,minY=h,maxX=-1,maxY=-1;
      for (int y=0;y<h;y++) for (int x=0;x<w;x++) {
        if (a[Idx(x,y,w)]<20) continue;
        if (x<minX)minX=x; if(y<minY)minY=y; if(x>maxX)maxX=x; if(y>maxY)maxY=y;
      }
      if (maxX<minX) throw new Exception("empty");

      int cw=maxX-minX+1, ch=maxY-minY+1;
      float scale = Math.Max(size/(float)cw, size/(float)ch) * overscan;
      int dw = Math.Max(1, (int)Math.Round(cw * scale));
      int dh = Math.Max(1, (int)Math.Round(ch * scale));
      int ox = (dw - size) / 2;
      int oy = (dh - size) / 2;

      using (Bitmap outBmp = new Bitmap(size, size, PixelFormat.Format32bppArgb)) {
        BitmapData od = outBmp.LockBits(new Rectangle(0,0,size,size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int os = Math.Abs(od.Stride);
        byte[] dest = new byte[os*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++) {
          int sx = Math.Min(cw-1, Math.Max(0, (int)((x + ox + 0.5f) / dw * cw)));
          int sy = Math.Min(ch-1, Math.Max(0, (int)((y + oy + 0.5f) / dh * ch)));
          int si = Idx(minX+sx, minY+sy, w);
          int o = y*os + x*4;
          byte aa=a[si], rr=r[si], gg=g[si], bb=b[si];
          if (IsHotMagenta(rr,gg,bb,aa) || IsFringe(rr,gg,bb,aa)) { aa=0; rr=0; gg=0; bb=0; }
          dest[o]=bb; dest[o+1]=gg; dest[o+2]=rr; dest[o+3]=aa;
        }
        for (int y=0;y<size;y++) for (int x=0;x<size;x++) {
          int o=y*os+x*4;
          if (dest[o+3] >= 20) continue;
          bool found=false;
          for (int rad=1; rad<96 && !found; rad++) {
            for (int dy=-rad; dy<=rad && !found; dy++)
            for (int dx=-rad; dx<=rad && !found; dx++) {
              if (Math.Abs(dx)!=rad && Math.Abs(dy)!=rad) continue;
              int nx=x+dx, ny=y+dy;
              if ((uint)nx>=(uint)size || (uint)ny>=(uint)size) continue;
              int no=ny*os+nx*4;
              if (dest[no+3] < 20) continue;
              dest[o]=dest[no]; dest[o+1]=dest[no+1]; dest[o+2]=dest[no+2]; dest[o+3]=255;
              found=true;
            }
          }
          if (!found) { dest[o]=42; dest[o+1]=36; dest[o+2]=32; dest[o+3]=255; }
        }
        for (int y=0;y<size;y++) for (int x=0;x<size;x++) {
          int o=y*os+x*4;
          byte rr=dest[o+2], gg=dest[o+1], bb=dest[o];
          if (IsHotMagenta(rr,gg,bb,255) || IsFringe(rr,gg,bb,255)) {
            dest[o]=48; dest[o+1]=40; dest[o+2]=36; dest[o+3]=255;
          }
        }
        Marshal.Copy(dest, 0, od.Scan0, dest.Length);
        outBmp.UnlockBits(od);
        string tmp = dstPng + ".tmp.png";
        outBmp.Save(tmp, ImageFormat.Png);
        File.Copy(tmp, dstPng, true);
        File.Copy(tmp, dstBytes, true);
        File.Delete(tmp);
      }
      Console.WriteLine("crop {0}x{1} -> {2}", cw, ch, size);
    }
  }
}
