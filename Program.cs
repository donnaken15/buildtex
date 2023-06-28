using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using Scene = Zones.Scene;
using GFX = Zones.GFX;
using Img = Zones.RawImg;
using V4 = Zones.Scene.Vec4;

class Program
{
	static V4 parseV4(string str)
	{
		string[] fls = str.Split(new char[]{','},4,StringSplitOptions.RemoveEmptyEntries);
		V4 v = new V4();
		for (int i = 0; i < fls.Length; i++)
		{
			v[i] = Convert.ToSingle(fls[i]);
		}
		return v;
	}
	static int[] atoia(string str)
	{
		string[] sl = str.Split(new char[]{','},StringSplitOptions.RemoveEmptyEntries);
		int[] ia = new int[sl.Length];
		for (int i = 0; i < sl.Length; i++)
		{
			ia[i] = Convert.ToInt32(sl[i]);
		}
		return ia;
	}
	static Size atos(string str)
	{
		int[] dims = atoia(str);
		return new Size(dims[0], dims[1]);
	}
	static Size nullsize = new Size(0,0);
	static uint align(uint addr, byte b)
	{
		b = (byte)(1 << b);
		byte m = (byte)(b-1);
		byte a = (byte)(addr);
		if ((a&m) != 0)
			return (byte)(b-(a&m));
		return 0;
	}
	
	public static void Main(string[] args)
	{
		Console.WriteLine("buildtex2 - donnaken15\n");
		string target = "__config.ini", outscn = "__output.scn", outtex = "__output.tex";
		bool foundini = File.Exists(target);
		if (!foundini)
			if (args.Length == 2)
			{
				target = args[1];
				foundini = File.Exists(target);
			}
		if (!foundini)
		{
			Console.WriteLine("No INI found for tex building.\n");
			Console.WriteLine("Usage (config.ini):\nExample section:\n");
			Console.WriteLine("[MyMaterial]\nTemplate=ImmediateMode_UI\nBlend=Blend");
			Console.WriteLine("Texture=mytex.png\nTexName=images\\highway\\gem2D_green.img\nColor=1.0,1.0,1.0,1.0");
			//Console.ReadKey();
		}
		IniFile ini = new IniFile();
		ini.Load(target);
		Scene scn = new Scene();
		GFX gfx = new GFX();
		int ii = 0;
		foreach (IniFile.IniSection s in ini.Sections)
		{
			if (!s.Name.StartsWith("__GH3PlusTex_"))
			{
				Console.WriteLine("[" + ++ii + "/" + ini.Sections.Count + "] "+s.Name);
				Scene.Mat m = new Scene.Mat(); // im stupid
				IniFile.IniSection.IniKey k;
				
				// get section name/material and template key
				m.Material = QbKey.Create(s.Name);
				if ((k = s.GetKey("Template")) == null)
					throw new MissingFieldException("A template name is required for this material: "+s.Name);
				m.Shader = QbKey.Create(k.Value);
				
				List<V4> preList = new List<V4>(), postList = new List<V4>();
				List<QbKey> texList = new List<QbKey>();
				if (m.Shader.Crc == 0x98d259f8)
				{
					// if template is animated texture, get props via dedicated names
					// just to not blindly change ambiguously named Props keyvalues
					string[] animStrings = new string[] {
						"UCells", "VCells", "FPS",
						"Offset", "StartFade", "EndFade"
					};
					for (int i = 0; i < animStrings.Length; i++)
					{
						if ((k = s.GetKey(animStrings[i])) != null)
							preList.Add(parseV4(k.Value));
					}
				}
				// get preProps by number until it can't find anymore
				while ((k = s.GetKey("PreProps"+preList.Count)) != null)
					preList.Add(parseV4(k.Value));
				
				// check if template uses color props
				bool colored = false;
				colored = (m.Shader.Crc == 0x98d259f8 ||
					m.Shader.Crc == 0x18564175 ||
					m.Shader.Crc == 0x274943ee);
				if (colored)
					if ((k = s.GetKey("Color")) != null)
						postList.Add(parseV4(k.Value));
				// get postProps
				while ((k = s.GetKey("PostProps"+postList.Count)) != null)
					postList.Add(parseV4(k.Value));
				
				// get texture list (typically has 1, because highway sprites)
				List<string> texNames = new List<string>();
				List<QbKey> texKeys = new List<QbKey>();
				List<Size> texScale = new List<Size>(); // >:(
				List<Size> texClip = new List<Size>();
				// this needs to be optimized or something
				if ((k = s.GetKey("Texture")) != null)
				{
					texList.Add(QbKey.Create(k.Value));
					texNames.Add(k.Value);
					string texkeystr = k.Value;
					if ((k = s.GetKey("TexName")) != null)
						texkeystr = k.Value;
					texKeys.Add(QbKey.Create(texkeystr));
					Size scale = nullsize;
					if ((k = s.GetKey("TexScale")) != null)
						scale = atos(k.Value);
					texScale.Add(scale);
					Size clip = scale;
					if ((k = s.GetKey("TexClip")) != null)
						clip = atos(k.Value);
					texClip.Add(clip);
				}
				while ((k = s.GetKey("Texture"+texList.Count)) != null)
				{
					texList.Add(QbKey.Create(k.Value));
					texNames.Add(k.Value);
					string texkeystr = k.Value;
					if ((k = s.GetKey("TexName"+texList.Count)) != null)
						texkeystr = k.Value;
					texKeys.Add(QbKey.Create(texkeystr));
					Size scale = nullsize;
					if ((k = s.GetKey("TexScale"+texList.Count)) != null)
						scale = atos(k.Value);
					texScale.Add(scale);
					Size clip = scale;
					if ((k = s.GetKey("TexClip"+texList.Count)) != null)
						clip = atos(k.Value);
					texClip.Add(clip);
				}
				
				// append these to material struct
				
				// --------------
				// ---- PRE  ----
				// --------------
				m.prePropCount = preList.Count;
				m.prePropOffset = m.ssize;
				m.ssize += (m.prePropCount<<4);
				for (int i = 0; i < preList.Count; i++)
					m.CopyVec(preList[i], m.prePropOffset+(i<<4));
				// --------------
				// ---- POST ----
				// --------------
				m.postPropCount = postList.Count;
				m.postPropOffset = m.ssize;
				m.ssize += (m.postPropCount<<4);
				for (int i = 0; i < postList.Count; i++)
					m.CopyVec(postList[i], m.postPropOffset+(i<<4));
				
				m.texCount = texList.Count;
				m.texOffset = m.ssize;
				m.ssize += (m.texCount<<2) + (int)align((uint)(m.texCount<<2), 4);
				
				// add specified textures to GFX
				for (int i = 0; i < texNames.Count; i++)
				{
					string ext = null;
					foreach (string e in new string[] {"png","dds","jpg","jpeg","bmp","tif","tiff"})
						if (File.Exists(texNames[i]+'.'+e))
							{ ext = '.' + e; break; }
					if (ext == null)
						throw new FileNotFoundException(
							"Couldn't find the texture "+texNames[i]+", from material "+s.Name+'.',texNames[i]);
					Img img = Img.MakeFromRaw(texNames[i]+ext);
					img.Name = texKeys[i];
					if (!texScale[i].Equals(nullsize))
					{
						img.widthScale = (ushort)texScale[i].Width;
						img.heightScale = (ushort)texScale[i].Height;
					}
					if (!texClip[i].Equals(nullsize))
					{
						img.widthClip = (ushort)texClip[i].Width;
						img.heightClip = (ushort)texClip[i].Height;
					}
					if (gfx.IndexOf(img.Name.Crc) == -1)
						gfx.Add(img);
				}
				
				for (int i = 0; i < texList.Count; i++)
					m.CopyInt(texKeys[i].Crc, m.texOffset+(i<<2));
				m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Blend", "0")),
					Scene.Mat.toffset + 0x38);
				m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Technique", "0")),
					Scene.Mat.toffset + 0x28);
				// unknown or useless, or both :P
				m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Bloom", "0")), Scene.Mat.toffset + 0x3C);
				m.CopyFloat(float.Parse(ini.GetKeyValue(s.Name, "Unk", "0.0")), Scene.Mat.toffset + 0x34);
				m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Flags", "00000000"),
						System.Globalization.NumberStyles.HexNumber), Scene.Mat.toffset + 0x2C);
				m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Flags2", "00000000"),
						System.Globalization.NumberStyles.HexNumber), Scene.Mat.toffset + 0x48);
				scn.Add(m);
			}
			else
			{
				Console.WriteLine("[" + ++ii + "/" + ini.Sections.Count + "] "+s.Name.Substring(13));
				IniFile.IniSection.IniKey k;
				if ((k = s.GetKey("Texture")) != null)
				{
					string file = k.Value;
					string texkeystr = k.Value;
					if ((k = s.GetKey("TexName")) != null)
						texkeystr = k.Value;
					Size scale = nullsize;
					if ((k = s.GetKey("TexScale")) != null)
						scale = atos(k.Value);
					Size clip = scale;
					if ((k = s.GetKey("TexClip")) != null)
						clip = atos(k.Value);
					string ext = null;
					foreach (string e in new string[] {"png","dds","jpg","jpeg","bmp","tif","tiff"})
						if (File.Exists(file+'.'+e))
							{ ext = '.' + e; break; }
					if (ext == null)
						throw new FileNotFoundException(
							"Couldn't find the texture "+file+", from material "+s.Name+'.',file);
					Img img = Img.MakeFromRaw(file+ext);
					img.Name = QbKey.Create(texkeystr);
					if (scale != nullsize)
					{
						img.widthScale = (ushort)scale.Width;
						img.heightScale = (ushort)scale.Height;
					}
					if (clip != nullsize)
					{
						img.widthClip = (ushort)clip.Width;
						img.heightClip = (ushort)clip.Height;
					}
					if (gfx.IndexOf(img.Name.Crc) == -1)
						gfx.Add(img);
				}
			}
		}
		// write SCN to file
		if (File.Exists(outscn))
			File.Delete(outscn);
		FileStream f = File.Open(outscn, FileMode.CreateNew);
		BinaryWriter b = new BinaryWriter(f);
		b.Write(0);
		for (int i = 0; i < 7; i++)
			b.Write(Zones.Eswap((uint)0xFAAABACA));
		b.Write((byte)4); // whatever
		b.Write((byte)0x10);
		b.Write(Zones.Eswap((ushort)scn.Count));
		b.Write((uint)Zones.Eswap(scn.matsSize()));
		b.Write(Zones.Eswap(0x10));
		b.Write(0xFFFFFFFF);
		for (int i = 0; i < scn.Count; i++)
		{
			b.Write(scn[i].data);
		}
		// extra useless
		b.Write(Zones.Eswap(0xBABEFACE));
		b.Write(Zones.Eswap(8));
		b.Write(0);
		b.Write(Zones.Eswap(0xEA));
		for (int i = 0; i < 12; i++)
			b.Write(0);
		uint randomPointer = (uint)f.Position;
		b.Write(Zones.Eswap(0xE0090));
		b.Write(0);
		b.Write(0);
		b.Write(Zones.Eswap(randomPointer+0x50)); // wtf is this
		b.Write(0);
		b.Write(0xFFFFFFFF);
		b.Write(Zones.Eswap(0x90));
		b.Write(0);
		b.Write(Zones.Eswap(0x90));
		b.Write(0);
		b.Write(0);
		b.Write(0);
		b.Write(0);
		b.Write(0xFFFFFFFF);
		b.Write(0);
		b.Write(0);
		b.Write(Zones.Eswap(0x90));
		b.Write(Zones.Eswap(0x90));
		b.Write(0xFFFFFFFF);
		b.Write(Zones.Eswap(0x90));
		b.Write(Zones.Eswap(0x90));
		b.Write(0);
		b.Write(0);
		b.Write(Zones.Eswap(0x90));
		for (int i = 0; i < 4; i++)
			b.Write(0xAAAAAAAA);
		b.Close();
		f.Close();
		if (File.Exists(outtex))
			File.Delete(outtex);
		f = File.Open(outtex, FileMode.CreateNew);
		b = new BinaryWriter(f);
		b.Write(Zones.Eswap(GFX.magic));
		b.Write(Zones.Eswap(GFX.const1));
		b.Write(Zones.Eswap((ushort)gfx.Count));
		b.Write(Zones.Eswap(gfx.startPos));
		b.Write(Zones.Eswap(gfx.szrel));
		b.Write(0xFFFFFFFF);
		b.Write(Zones.Eswap(gfx.texLog));
		b.Write(Zones.Eswap(0x1C));
		while (f.Position < gfx.startPos)
			b.Write(0xEFEFEFEF);
		uint curimgptr = (uint)(gfx.startPos + (gfx.Count * 0x28));
		for (int i = 0; i < gfx.Count; i++)
		{
			b.Write(Zones.Eswap(0x0A280200));
			b.Write(Zones.Eswap(gfx[i].head.key));
			b.Write(Zones.Eswap(gfx[i].head.w_scale));
			b.Write(Zones.Eswap(gfx[i].head.h_scale));
			b.Write(Zones.Eswap(gfx[i].head.unk0));
			b.Write(Zones.Eswap(gfx[i].head.w_clip));
			b.Write(Zones.Eswap(gfx[i].head.h_clip));
			b.Write(Zones.Eswap(gfx[i].head.unk1));
			b.Write(gfx[i].head.mipmaps);
			b.Write(gfx[i].head.bpp);
			b.Write(gfx[i].head.compression);
			b.Write(gfx[i].head.unk2);
			b.Write(Zones.Eswap(gfx[i].head.unk3));
			b.Write(Zones.Eswap(curimgptr));
			b.Write(Zones.Eswap(gfx[i].head.size));
			b.Write(Zones.Eswap(gfx[i].head.unk4));
			curimgptr += (uint)(gfx[i].rawimg.Length);
		}
		for (int i = 0; i < gfx.Count; i++)
			b.Write(gfx[i].rawimg);
		b.Close();
		f.Close();
		GC.Collect();
	}
}
