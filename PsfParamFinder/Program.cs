using System;
using System.Collections.Generic;
using System.IO;
//using System.Buffers.Binary;
//using ComponentAce.Compression.Libs.zlib;
using Ionic.Zlib;

namespace PsfParamFinder
{
    public enum PsfTypes
    {
        EXE,
        PSF,
        MINIPSF
    }
    public class PsfFile
    {
        public bool modified;
        public string filename;
        public byte[] headersect;
        public uint start;
        public uint end;
        public uint crc;
        public byte[] reserved_area;
        public byte[] tags;
    }
    public class PsfTable
    {
        public PsfTypes ftype;
        public byte[] ram;
        public List<PsfFile> minipsfs;
    }
    [Serializable]
    public class InternalParams
    {
        public uint offset;
        public int sig;
        public uint loadaddr;
        public uint entrypoint;
        public string drivername;
        public string exename;
        public uint crc;
        public uint jumppatch;
        public List<SongArea> blocks;
        public Dictionary<string,PsfParameter> psfparams;
    }
    [Serializable]
    public struct SongArea
    {
        public uint addr;
        public uint size;
    }

    [Serializable]
    public struct PsfParameter
    {
        
        public int loc;
        public byte[] value;
        public PsfParameter(int l, byte[] v)
        {
            loc = l;
            value = new byte[v.Length];
            v.CopyTo(value, 0);
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            PsfTable mem = LoadFile(args[0]);

            MemoryStream fstream = new MemoryStream(mem.ram);
            InternalParams testpar = binvals(fstream);
            PsfParameter pp;
            if (testpar == null)
            {
                return;
            }
            foreach (SongArea sa in testpar.blocks)
            {
                Console.WriteLine("Saved Block - ADDR: {0} SIZE: {1}", sa.addr, sa.size);
            }
            foreach (string sp in testpar.psfparams.Keys)
            {
                if(testpar.psfparams.TryGetValue(sp, out pp))
                {
                    Console.WriteLine("Name: {0} Number of Bytes: {1} Value: {2}", sp, pp.value.Length, BitConverter.ToString(pp.value));
                    if (pp.value.Length == 4)
                    {
                        
                        Console.WriteLine("OFFSET VALUE: {0:X}", BitConverter.ToUInt32(pp.value) - testpar.offset);
                    }
                }

            }
            
        }
        static string nullterm(string s, int index)
        {
            return s.Substring(index, s.IndexOf('\0', index) - index);
        }

        static string[] psflibs(BinaryReader br, int tagpos)
        {
            List<string> liblines = new List<string>();
            StreamReader sr = new StreamReader(br.BaseStream);
            string lib = "";
            if (tagpos < 0)
            {
                br.BaseStream.Seek(4, SeekOrigin.Begin);
                uint rsize = br.ReadUInt32();
                uint psize = br.ReadUInt32();
                br.BaseStream.Seek(16 + psize + rsize, SeekOrigin.Begin);
            }
            else
            {
                br.BaseStream.Seek(tagpos, SeekOrigin.Begin);
            }

            
            uint tagsig = br.ReadUInt32();
            
            if (tagsig == 0x4741545B && br.ReadByte() == 0x5D)
            {
                while (sr.Peek() >= 0)
                {
                    try
                    {
                        lib = sr.ReadLine();
                        //libs = lib.Split('=');
                        //Console.WriteLine(lib);
                        if (lib.ToLowerInvariant().StartsWith("_lib"))
                        {
                            //mini = true;
                            //Console.WriteLine("{0} - is a minipsf", lib);
                            //break;
                            liblines.Add(lib);
                        }
                    }
                    catch (Exception tx)
                    {
                        Console.WriteLine("Exception: {0}", tx.Message);
                        Console.WriteLine("{0} was not a valid tag line", lib);
                    }

                }
            }
            liblines.Sort();
            List<string> libs = new List<string>();

            foreach(string ls in liblines)
            {
                try
                {
                    libs.Add(ls.Split('=', StringSplitOptions.RemoveEmptyEntries)[1]);
                }
                catch (Exception lx)
                {
                    Console.WriteLine("{0} was not a valid library", ls);
                    Console.WriteLine("Exception: {0}", lx.Message);
                }
            }
            return libs.ToArray();
        }

        static PsfTable LoadFile(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);
            uint ftype = br.ReadUInt32();
            PsfTable pt = null;
            switch (ftype)
            {
                case 0x01465350: //PSF
                    string[] pl = psflibs(br, -1);
                    if (pl.Length > 0)
                    {
                        fs.Dispose();
                        pt = LoadMiniPsf(filename);
                    }
                    else
                    {
                        pt = LoadPsf(br);
                        fs.Dispose();
                    }

                    break;
                case 0x582D5350: //PSX EXE
                    pt = LoadExe(br);
                    fs.Dispose();
                    break;
                default:
                    Console.WriteLine("{0} is not a readable PSF or EXE file!", filename);
                    break;
                    
            }
            
            return pt;
        }

        

        static PsfTable LoadMiniPsf(string filename)
        {
            return null;
        }

        static PsfTable LoadPsf(BinaryReader f)
        {
            return null;
        }

        static PsfTable LoadExe(BinaryReader b)
        {
            
            b.BaseStream.Seek(0, SeekOrigin.Begin);
            PsfTable psf = new PsfTable();
            psf.ftype = PsfTypes.EXE;
            psf.ram = b.ReadBytes((int)b.BaseStream.Length);
            return psf;
        }
        static InternalParams binvals(Stream fs)
        {
            try
            {
                uint param, param2, ipoffset, tparam, tparam2;
                int param3;
                long postemp;
                
                BinaryReader br = new BinaryReader(fs);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
                InternalParams ip = new InternalParams();
                ip.sig = psfexe.IndexOf("PSF_DRIVER_INFO:");
                fs.Seek(24, SeekOrigin.Begin);
                ip.offset = br.ReadUInt32() - 2048;
                ipoffset = ip.offset % 0x20000000;
                fs.Seek(ip.sig + 16, SeekOrigin.Begin);
                ip.loadaddr = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.entrypoint = br.ReadUInt32() % 0x20000000 - ipoffset;
                param = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.drivername = nullterm(psfexe, (int)param);
                param = br.ReadUInt32() % 0x20000000 - ipoffset;
                ip.exename = nullterm(psfexe, (int)param);
                ip.crc = br.ReadUInt32();
                ip.jumppatch = br.ReadUInt32() % 0x20000000 - ipoffset;
                SongArea sa;
                param = br.ReadUInt32();
                param2 = br.ReadUInt32();
                sa.addr = param % 0x20000000 - ipoffset;
                sa.size = param2;
                ip.blocks = new List<SongArea>();
                while (param != 0 && param2 != 0)
                {
                    ip.blocks.Add(sa);
                    param = br.ReadUInt32();
                    param2 = br.ReadUInt32();
                    sa.addr = param % 0x20000000 - ipoffset;
                    sa.size = param2;
                }
                ip.psfparams = new Dictionary<string, PsfParameter>();
                fs.Seek(-4, SeekOrigin.Current);
                tparam = br.ReadUInt32();
                tparam2 = br.ReadUInt32();
                param = tparam % 0x20000000 - ipoffset;
                param2 = tparam2 % 0x20000000 - ipoffset;
                param3 = br.ReadInt32();
                postemp = fs.Position;
                while (tparam != 0 && tparam2 != 0 && param3 != 0)
                {
                    fs.Seek(param2, SeekOrigin.Begin);
                    PsfParameter pp = new PsfParameter((int)param, br.ReadBytes(param3));
                    ip.psfparams.Add(nullterm(psfexe, (int)param), pp);
                    fs.Seek(postemp, SeekOrigin.Begin);
                    tparam = br.ReadUInt32();
                    tparam2 = br.ReadUInt32();
                    param = tparam % 0x20000000 - ipoffset;
                    param2 = tparam2 % 0x20000000 - ipoffset;
                    param3 = br.ReadInt32();
                    postemp = fs.Position;
                    
                }
                return ip;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return null;
        }
    }
}
