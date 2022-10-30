using System;
using System.Collections.Generic;
using System.IO;
namespace PsfParamFinder
{
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
        /*
        public InternalParams()
        {
            //exetext = 0;
            //loadaddr = 0;
            //entrypoint = 0;
            //drivername = "";
            //exename = "";
            //crc = 0;
            //jumppatch = 0;
            blocks = new List<SongArea>();
        }
        */
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
            InternalParams testpar = binvals(args[0]);
            PsfParameter pp;

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

        static InternalParams binvals(string file)
        {
            try
            {
                uint param, param2;
                int param3;
                long postemp;
                FileStream fs = File.Open(file, FileMode.Open);
                BinaryReader br = new BinaryReader(fs);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
                InternalParams ip = new InternalParams();
                ip.sig = psfexe.IndexOf("PSF_DRIVER_INFO:");
                
                fs.Seek(24, SeekOrigin.Begin);
                ip.offset = br.ReadUInt32() - 2048;
                fs.Seek(ip.sig + 16, SeekOrigin.Begin);
                ip.loadaddr = br.ReadUInt32() - ip.offset;
                ip.entrypoint = br.ReadUInt32() - ip.offset;
                param = br.ReadUInt32() - ip.offset;
                ip.drivername = nullterm(psfexe, (int)param);
                param = br.ReadUInt32() - ip.offset;
                ip.exename = nullterm(psfexe, (int)param);
                ip.crc = br.ReadUInt32();
                ip.jumppatch = br.ReadUInt32() - ip.offset;
                SongArea sa;
                param = br.ReadUInt32();
                param2 = br.ReadUInt32();
                sa.addr = param - ip.offset;
                sa.size = param2;
                ip.blocks = new List<SongArea>();
                while (param != 0 && param2 != 0)
                {
                    ip.blocks.Add(sa);
                    param = br.ReadUInt32();
                    param2 = br.ReadUInt32();
                    sa.addr = param - ip.offset;
                    sa.size = param2;
                }
                ip.psfparams = new Dictionary<string, PsfParameter>();
                fs.Seek(-4, SeekOrigin.Current);
                param = br.ReadUInt32() - ip.offset;
                param2 = br.ReadUInt32() - ip.offset;
                param3 = br.ReadInt32();
                postemp = fs.Position;
                fs.Seek(param2, SeekOrigin.Begin);

                while (param != 0 && param2 != 0 && param3 != 0)
                {
                    PsfParameter pp = new PsfParameter((int)param, br.ReadBytes(param3));
                    ip.psfparams.Add(nullterm(psfexe, (int)param), pp);
                    fs.Seek(postemp, SeekOrigin.Begin);

                    param = br.ReadUInt32() - ip.offset;
                    param2 = br.ReadUInt32() - ip.offset;
                    param3 = br.ReadInt32();
                    postemp = fs.Position;
                    fs.Seek(param2, SeekOrigin.Begin);
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
