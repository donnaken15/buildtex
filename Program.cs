using System;
using System.IO;
using System.Collections.Generic;
using Scene = Zones.Scene;
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
		string target = "__config.ini";
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
		int ii = 0;
		foreach (IniFile.IniSection s in ini.Sections)
		{
			Console.WriteLine("[" + ++ii + "/" + ini.Sections.Count + "] "+s.Name);
			Scene.Mat m = new Scene.Mat(); // im stupid
			IniFile.IniSection.IniKey k;
			m.Material = QbKey.Create(s.Name);
			if ((k = s.GetKey("Template")) == null)
				throw new MissingFieldException("A template name is required for this material: "+s.Name);
			m.Shader = QbKey.Create(k.Value);
			List<V4> preList = new List<V4>(), postList = new List<V4>();
			List<QbKey> texList = new List<QbKey>();
			if (m.Shader.Crc == 0x98d259f8)
			{
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
			while ((k = s.GetKey("PreProps"+preList.Count)) != null)
				preList.Add(parseV4(k.Value));
			bool colored = false;
			colored = (m.Shader.Crc == 0x98d259f8 ||
				m.Shader.Crc == 0x18564175 ||
				m.Shader.Crc == 0x274943ee);
			if (colored)
				if ((k = s.GetKey("Color")) != null)
					postList.Add(parseV4(k.Value));
			m.CopyFloat(float.Parse(ini.GetKeyValue(s.Name, "Unk", "0.0")),
				Scene.Mat.toffset + 0x34);
			while ((k = s.GetKey("PostProps"+postList.Count)) != null)
				postList.Add(parseV4(k.Value));
			m.prePropCount = preList.Count;
			m.prePropOffset = m.ssize;
			m.ssize += (m.prePropCount<<4);
			for (int i = 0; i < preList.Count; i++)
				m.CopyVec(preList[i], m.prePropOffset+(i<<4));
			m.postPropCount = postList.Count;
			m.postPropOffset = m.ssize;
			m.ssize += (m.postPropCount<<4);
			for (int i = 0; i < postList.Count; i++)
				m.CopyVec(postList[i], m.postPropOffset+(i<<4));
			if ((k = s.GetKey("Texture")) != null)
				texList.Add(QbKey.Create(k.Value));
			while ((k = s.GetKey("Texture"+texList.Count)) != null)
				texList.Add(QbKey.Create(k.Value));
			m.texCount = texList.Count;
			m.texOffset = m.ssize;
			m.ssize += (m.texCount<<2) + (int)align((uint)(m.texCount<<2), 4);
			for (int i = 0; i < texList.Count; i++)
				m.CopyInt(texList[i].Crc, m.texOffset+(i<<2));
			m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Blend", "0")),
				Scene.Mat.toffset + 0x38);
			m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Technique", "0")),
				Scene.Mat.toffset + 0x28);
			m.CopyInt(int.Parse(ini.GetKeyValue(s.Name, "Bloom", "0")),
				Scene.Mat.toffset + 0x3C);
			m.CopyInt(
				int.Parse(ini.GetKeyValue(s.Name, "Flags", "00000000"),
					System.Globalization.NumberStyles.HexNumber),
				Scene.Mat.toffset + 0x2C);
			m.CopyInt(
				int.Parse(ini.GetKeyValue(s.Name, "Flags2", "00000000"),
					System.Globalization.NumberStyles.HexNumber),
				Scene.Mat.toffset + 0x48);
			scn.Add(m);
		}
		if (File.Exists("__output.scn"))
			File.Delete("__output.scn");
		FileStream f = File.Open("__output.scn", FileMode.CreateNew);
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
		f.Close();
		GC.Collect();
	}
}
