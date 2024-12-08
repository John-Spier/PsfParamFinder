using System;
using System.Collections.Generic;
using System.IO;
//using System.Buffers.Binary;
//using ComponentAce.Compression.Libs.zlib;
using Ionic.Zlib;
using Force.Crc32;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Text;
using System.Linq;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;


//using System.Runtime.Serialization.Formatters.Binary;
//use ipaddress fucntions to change endians?
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
        public uint segment;
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
        public uint drivernameloc;
        public uint exenameloc;
        public List<SongArea> blocks;
        public Dictionary<string,PsfParameter> psfparams;
    }
    [Serializable]
    public struct SongArea
    {
        public uint addr;
        public uint size;
    }

    public struct PsfSection
    {
        public uint loc;
        public uint layer;
        public bool start;
    }

    [Serializable]
    public struct PsfParameter
    {
        
        public uint loc;
        public byte[] value;
        public uint nameloc;
        public PsfParameter(uint l, uint n, byte[] v)
        {
            loc = l;
            nameloc = n;
            value = new byte[v.Length];
            v.CopyTo(value, 0);
        }
    }

    [Serializable]
    public struct ParamsV1
    {
        public short SeqNum;
        public short Version;
        public short MvolL;
        public short MvolR;
        public short VolL;
        public short VolR;
        public short RvolL;
        public short RvolR;
        public short RdepthL;
        public short RdepthR;
        public short Rdelay;
        public short Rmode;
        public short Rfeedback;
        public int TickMode;
        public char SeqFlags;
        public char SeqType;
    }

    [Serializable]
    public struct SeqInfo
    {
		public int seqstart;
		public int seqend;
		public bool is_sep;
        public int sep_file;
        public int file_track;
        public bool seq_from_info;
        public bool enabled;
        public int priority;
        public string md5;
	}
	[Serializable]
	public struct SepInfo
    {
        public int sepstart;
        public int sepend;
		public bool sep_from_info;
		public string md5;
	}
    [Serializable]
    public struct VabInfo
    {
        public int vhstart;
        public int vhend;
        public int vbstart;
        public int vbend;
        public decimal vbprob;
		public bool vh_from_info;
		public bool vb_from_info;
        public bool vb_not_found;
		public string md5;
	}
    [Serializable]
	public struct VhInfo
    {
        public int vh;
        public short vagnum;
        public int[] vags;
        public int vh_size;
        public int vb_size;
        public int vag_size;
        public bool vh_from_info;
    }

	[Serializable]
	public struct SoundInfo
	{
        public SeqInfo[] seq;
        public SepInfo[] sep;
        public VabInfo[] vab;
        public int sep_main_track;
        public int seq_priority;
        public string source_filename;
        public string name;
		public InternalParams int_params;
	}
    [Serializable]
    public struct VFSFile
    {
        public string source;
        public string name;
        public int file1_start;
		public int file1_end;
		public int file2_start;
		public int file2_end;
        public bool use_params;
        public uint filetype;
        public short params_ver;
        public bool is_sep;
        public InternalParams int_params;
	}

	class Program
    {
        static void Main(string[] args)
        {
            VFSFile[] json = SaveSoundFiles(".\\EXE", "*.exe");
            JsonSerializerOptions options = new(JsonSerializerDefaults.General)
            {
                WriteIndented = true,
                IncludeFields = true
            };
            string jstring = JsonSerializer.Serialize(json, options);
            //File.WriteAllText("TEST.JSON", jstring);
            VFSFile[] import_json = JsonSerializer.Deserialize<VFSFile[]>(jstring, options);
            string jstring2 = JsonSerializer.Serialize(import_json, options);
			//File.WriteAllText("TEST2.JSON", jstring2);
			if (json.SequenceEqual(import_json))
			{
                Console.WriteLine();
				Console.WriteLine("JSON SUCCESS!");
			}
			return;

        }
        static VFSFile[] SaveSoundFiles(string dir, string pattern = "*.psf", short params_ver = 0, bool use_all_combinations = false,
            SearchOption so = SearchOption.AllDirectories, bool brute = false, bool verbose = false, bool use_largest_seq = false,
            bool strict = true, bool prioritize_info = true, TextWriter con = null)
        {
            if (con == null)
            {
				con = Console.Out;
			}


			List<SoundInfo> psffiles = new();
            Dictionary<string, int> vabmd5 = new(); //Value = Index in vabfiles
			Dictionary<string, int> seqmd5 = new(); //Value = Number of times SEQ appears
			List<VFSFile> vabfiles = new();
			foreach (string file in Directory.EnumerateFiles(dir, pattern, so))
            {
                if (verbose)
                {
                    con.WriteLine("Loading {0}...", Path.GetFullPath(file));
				}
                SoundInfo info = GetSoundFiles(LoadFile(file), checkbrute: brute, prioritize_spec: strict, verbose: verbose, con: con);
                info.source_filename = Path.GetFullPath(file);
                if (verbose)
                {
                    con.WriteLine("{0} SEQs, {1} SEPs, {2} VABs, SEP Track {3}", info.seq.Length, info.sep.Length, info.vab.Length, info.sep_main_track);
                }
                psffiles.Add(info);
                foreach(VabInfo vi in info.vab)
                {
                    if (!vabmd5.ContainsKey(vi.md5))
                    {
                        vabmd5.Add(vi.md5, vabfiles.Count);
						VFSFile file1 = new()
						{
							source = info.source_filename,
							//params_ver = params_ver,
							use_params = false,
							name = "VAB #" + vabfiles.Count + ": " + info.name,
							//int_params = info.int_params,
							file1_start = vi.vhstart,
							file1_end = vi.vhend,
							file2_start = vi.vbstart,
							file2_end = vi.vbend,
                            is_sep = false,
							filetype = 0xFFFFFF0F //VAB
						};
                        if (verbose)
                        {
                            con.WriteLine("VAB from file {0} with MD5 {1} ({2} bytes) added", 
                                Path.GetFileName(file), vi.md5, vi.vhend - vi.vhstart + (vi.vbend - vi.vbstart));
                        }
                        vabfiles.Add(file1);
					}
                    else if (verbose)
                    {
                        con.WriteLine("VAB from file {0} with MD5 {1} ({2} bytes) not added, duplicate", 
                            Path.GetFileName(file), vi.md5, vi.vhend - vi.vhstart + (vi.vbend - vi.vbstart));
                    }
                }
                foreach (SeqInfo si in info.seq)
                {
                    if (seqmd5.ContainsKey(si.md5))
                    {
                        if (!use_largest_seq)
                        {
							seqmd5[si.md5]++;
						}
					}
                    else
                    {
                        seqmd5.Add(si.md5, 1);
                    }
					if (verbose)
					{
						con.WriteLine("SEQ from file {0} with MD5 {1} found, {2} time(s) total, {3} bytes, PSF_DRIVER_INFO: {4}, Priority {5}", 
                            Path.GetFileName(file), si.md5, seqmd5[si.md5], si.seqend - si.seqstart, si.seq_from_info, si.priority);
					}
				}
                if (verbose)
                {
                    con.WriteLine();
                    con.WriteLine();
                }
            }

			for (int i = 0; i < psffiles.Count; i++)
			{
                //This is for editing the infos to disable or enable the SEQ files
                int[] unique =
                {
                    int.MaxValue,
                    int.MaxValue,
                    int.MaxValue,
                    int.MaxValue
                };
				int size = -1, guess = -1, priority = -1;
                if (use_all_combinations)
                {
                    for (int j = 0; j < psffiles[i].seq.Length; j++)
                    {
                        psffiles[i].seq[j].enabled = true;
                    }
                }
                else
                {
                    for (int j = 0; j < psffiles[i].seq.Length; j++) //uniqueness checking loop
                    {
                        if (!prioritize_info && (psffiles[i].seq[j].priority & 1) == 1)
                        {
                            psffiles[i].seq[j].seq_from_info = false;
                            psffiles[i].seq[j].priority--;
                        }
                        if (seqmd5.TryGetValue(psffiles[i].seq[j].md5, out int md5temp) && md5temp < unique[psffiles[i].seq[j].priority])
                        {
                            unique[psffiles[i].seq[j].priority] = seqmd5[psffiles[i].seq[j].md5];
						}
                        /*
                        if (psffiles[i].seq[j].is_sep && psffiles[i].seq[j].file_track == psffiles[i].sep_main_track)
                        {
                            if (psffiles[i].seq[j].seq_from_info && seqmd5[psffiles[i].seq[j].md5] < unique_sep_info)
                            {
                                unique_sep_info = seqmd5[psffiles[i].seq[j].md5];
                                //psffiles[i].seq[j].priority = 3;

								if (priority < 3)
                                {
                                    priority = 3; 
                                }
							}
                            else if (seqmd5[psffiles[i].seq[j].md5] < unique_sep)
                            {
								unique_sep = seqmd5[psffiles[i].seq[j].md5];
								if (priority < 2)
								{
									priority = 2;
								}
							}
                        }
                        else if (psffiles[i].seq[j].seq_from_info && seqmd5[psffiles[i].seq[j].md5] < unique_info)
                        {
                            unique_info = seqmd5[psffiles[i].seq[j].md5];
							if (priority < 1)
							{
								priority = 1;
							}
						}
                        else if (seqmd5[psffiles[i].seq[j].md5] < unique)
                        {
							unique_info = seqmd5[psffiles[i].seq[j].md5];
							if (priority < 0)
							{
								priority = 0;
							}
						}
                        */
                    }

                    priority = -1;
                    for (int j = 0; j < unique.Length; j++)
                    {
                        if (unique[j] < int.MaxValue)
                        {
                            priority = j;
                        }
                    }
					size = -1;
					guess = 0;
					for (int j = 0; j < psffiles[i].seq.Length; j++) //size checking loop
                    {
                        if (psffiles[i].seq[j].priority == priority && seqmd5[psffiles[i].seq[j].md5] == unique[priority] 
                            && (psffiles[i].seq[j].seqend - psffiles[i].seq[j].seqstart) > size)
                        {
                            guess = j;
                            size = psffiles[i].seq[j].seqend - psffiles[i].seq[j].seqstart;
						}
                    }
                    psffiles[i].seq[guess].enabled = true;
                    if (!use_largest_seq)
                    {
                        seqmd5.Remove(psffiles[i].seq[guess].md5);
                    }
                    if (verbose)
                    {
                        con.WriteLine("SEQ {0} of file {1}, MD5 {2} selected with priority {3}, size {4} bytes, {5} total SEQs remain", 
                            guess, psffiles[i].name, psffiles[i].seq[guess].md5, priority, size, seqmd5.Count);
                    }

				}
			}
			foreach (SoundInfo sound in psffiles)
			{
                int tracknum = 1;
				foreach (SeqInfo seq in sound.seq)
				{
                    if (seq.enabled)
                    {
                        foreach (VabInfo vab in sound.vab)
                        {
                            VFSFile file1 = new()
                            {
                                is_sep = seq.is_sep,
                                params_ver = params_ver,
                                use_params = true,
                                int_params = sound.int_params,
                                source = sound.source_filename,
                                name = sound.name,
                                file1_start = seq.seqstart,
                                file1_end = seq.seqend,
                                filetype = 0x01020000 + (uint)vabmd5[vab.md5]
                            };
                            if (tracknum > 1)
                            {
                                file1.name += " (" + tracknum + ")";
                            }
                            tracknum++;
                            vabfiles.Add(file1);
                            if (verbose)
                            {
                                con.WriteLine("Added SEQ file at pos #{0}, base VAB #{1}, track {2}, SEP: {3}",
                                    vabfiles.Count - 1, vabmd5[vab.md5], file1.name, file1.is_sep);
                            }
                        }
                    }
                    else if (verbose)
                    {
                        con.WriteLine("SEQ file skipped from {0}", sound.name);
                    }
				}
			}

			return vabfiles.ToArray();
        }

        static SoundInfo GetSoundFiles(PsfTable table, bool checkall = true, bool verbose = false, bool checkbrute = false, 
            bool prioritize_spec = true, bool allow_vabp = false, bool allow_seqp = true, bool seq_vh_search_all = true, 
            decimal vb_correct_needed = (decimal)1, bool check_sample_ends = false, bool use_probability = false, 
            bool check_vab = true, TextWriter con = null)
        {
            
            MemoryStream rampar = new(table.ram);
            InternalParams ip = Binvals(rampar);
            SoundInfo si = new()
            {
                sep_main_track = -1,
                int_params = ip
            };
			int seqsearch = -4;

            //int vabsize, vbsize, vhsize, vagsize, vagnum;
            List<int> clist = new();
            if (con == null)
            {
                con = Console.Out;
            }

            if (!(ip == null || ip.blocks == null))
            {
                foreach (SongArea s in ip.blocks)
                {
                    clist.Add((int)(s.addr - ip.offset));
                    foreach (PsfFile psf in table.minipsfs)
                    {
                        if (s.addr - ip.offset + psf.start - 2048 < table.ram.Length - 16)
                        {
                            clist.Add((int)(s.addr - ip.offset + psf.start - 2048));
                        }
                    }
                }
            }
            try
            {
                if (!(ip == null || ip.psfparams == null))
                {
					if (ip.psfparams.TryGetValue("seqnum", out PsfParameter pp))
					{
						si.sep_main_track = pp.value.FirstOrDefault();
					}
					if (ip.psfparams.TryGetValue("SEQ/SEP/VAG Address", out pp))
                    {
                        clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
                    }
                    if (ip.psfparams.TryGetValue("VH Address", out pp))
                    {
                        clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
                    }
                    if (ip.psfparams.TryGetValue("VB Address", out pp))
                    {
                        clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
                    }
					if (ip.psfparams.TryGetValue("SEQ/SEP Mem Address", out pp))
					{
						clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
					}
					if (ip.psfparams.TryGetValue("VH Mem Address", out pp))
					{
						clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
					}
					if (ip.psfparams.TryGetValue("VB Mem Address", out pp))
					{
						clist.Add((int)(BitConverter.ToUInt32(pp.value) - ip.offset));
					}
				}
                si.name = FindName(table.minipsfs.Last());
            }
            catch (Exception ex)
            {
                if (verbose)
                {
                    con.WriteLine("Parameter error: {0}", ex.ToString());
                }
            }

            List<int> plist = clist.Distinct().ToList();
			int[] pcandidates = plist.ToArray();
            int param_num = pcandidates.Length;
			List<SeqInfo> seqs = new();
			List<SepInfo> seps = new();

            bool seqp = false;
            bool seq_from_params = true;

            List<int> seqfiles = new();

			while (seqsearch != -1)
			{
                SeqInfo seqInfo = new();
                SepInfo sepInfo = new();
                if (seqp)
                {
					seqInfo.seqstart = FindFile(table.ram, "SEQp", seqsearch + 4, pcandidates);
					if (verbose && seqInfo.seqstart != -1)
					{
						con.WriteLine("Warning: SEQp signature detected at {0}", seqInfo.seqstart);
					}
				}
                else
                {
					seqInfo.seqstart = FindFile(table.ram, "pQES", seqsearch + 4, pcandidates);
				}
                if (seqInfo.seqstart > 0 && !seqfiles.Contains(seqInfo.seqstart))
                {
                    seqfiles.Add(seqInfo.seqstart);

					if (BitConverter.ToInt32(table.ram, seqInfo.seqstart + 4) == 0x01000000)
					{
						seqInfo.is_sep = false;
						seqInfo.seqend = FindFile(table.ram, "\x00FF/\0", seqInfo.seqstart) + 3;

						if (!prioritize_spec || seqInfo.seqend == -1)
						{
							int seqend2;
							seqend2 = FindFile(table.ram, "\x00FF\0\0", seqInfo.seqstart) + 3;
							if (seqend2 > 0 && seqend2 < seqInfo.seqend)
							{
								if (verbose)
								{
									con.WriteLine("Warning: alternate sequence end detected before normal sequence end ({0}/{1})", seqend2, seqInfo.seqend);
								}
								seqInfo.seqend = seqend2;
							}

							seqend2 = FindFile(table.ram, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0", seqInfo.seqstart) + 16;
							if (seqend2 > 0 && seqend2 < seqInfo.seqend)
							{
								if (verbose)
								{
									con.WriteLine("Warning: alternate sequence end 2 detected before normal sequence end ({0}/{1})", seqend2, seqInfo.seqend);
								}
								seqInfo.seqend = seqend2;
							}
						}
						if (seqInfo.seqend > -1)
						{
                            seqInfo.md5 = GetMD5(table.ram, seqInfo.seqstart + 8, seqInfo.seqend);
                            seqInfo.seq_from_info = seq_from_params;
                            seqInfo.enabled = false;
                            seqInfo.priority = 0;
                            if (seq_from_params)
                            {
                                seqInfo.priority++;
                            }
							seqs.Add(seqInfo);
						}
					}
					else
					{
						int oldcount = seqs.Count;
						seqs.AddRange(CountSepTracks(table.ram, seqInfo.seqstart, prioritize_spec, seps.Count, seq_from_params, si.sep_main_track));
						if (seqs.Count > oldcount)
						{
							sepInfo.sepstart = seqInfo.seqstart;
							sepInfo.sepend = seqs.Last().seqend;
                            sepInfo.md5 = GetMD5(table.ram, sepInfo.sepstart, sepInfo.sepend);
                            sepInfo.sep_from_info = seq_from_params;
							seps.Add(sepInfo);
						}

					}
				}

				if (pcandidates.Length > 0)
				{
					plist.Remove(pcandidates[0]);
					pcandidates = plist.ToArray();
				}
				else
				{
					if (seq_vh_search_all)
					{
						seqsearch = seqInfo.seqstart;
					}
					else
					{
						seqsearch = -1;
					}
					if (allow_seqp && !seqp && seqsearch == -1)
					{
						seqp = true;
						plist = clist.Distinct().ToList();
						pcandidates = plist.ToArray();
					}
                    else
                    {
						seq_from_params = false;
					}
				}
			}

            int highest_priority = -1;
            foreach (SeqInfo seq in seqs)
            {
                if (seq.priority > highest_priority)
                {
                    highest_priority = seq.priority;
                }
            }
            si.seq_priority = highest_priority;

            si.seq = seqs.ToArray();
            si.sep = seps.ToArray();

            if (!check_vab)
            {
                return si;
            }

			plist = clist.Distinct().ToList();
			pcandidates = plist.ToArray();
            List<int> vhfiles = new();
            List<VhInfo> vagfiles = new();
            int maxvag = 0;
            int vhsearch = -4;
            bool vabp = false;
            bool strict_size = prioritize_spec;

            bool vh_from_params = true;

            while (vhsearch != -1)
            {
                VhInfo vh = new();
                if (vabp)
                {
					vh.vh = FindFile(table.ram, "VABp", vhsearch + 4, pcandidates);
                    if (verbose && vh.vh != -1)
                    {
                        con.WriteLine("Warning: VABp signature detected at {0}", vh.vh);
                    }
				}
                else
                {
					vh.vh = FindFile(table.ram, "pBAV", vhsearch + 4, pcandidates);
				}
				if (vh.vh > 0 && !vhfiles.Contains(vh.vh))
                {
					vh.vh_size = 0x20 + 0x800 + 0x200 + (BitConverter.ToInt16(table.ram, vh.vh + 18) * 0x200);
                    vh.vb_size = BitConverter.ToInt32(table.ram, vh.vh + 12) - vh.vh_size;
					vh.vagnum = BitConverter.ToInt16(table.ram, vh.vh + 22);

					vh.vags = new int[vh.vagnum];
					for (int i = 0; i < vh.vagnum; i++)
					{
						vh.vags[i] = BitConverter.ToUInt16(table.ram, vh.vh + vh.vh_size - 0x1FE + (i * 2)) * 8;
						vh.vag_size += vh.vags[i];
					}
                    if (!strict_size || (vh.vb_size == vh.vag_size))
                    {
                        if (verbose && vh.vag_size != vh.vb_size)
                        {
							con.WriteLine("WARNING: VH header total sample size/VB file size mismatch ({0}/{1})", vh.vag_size, vh.vb_size);
						}
						if (vh.vagnum > maxvag)
						{
							maxvag = vh.vagnum;
						}
                        vh.vh_from_info = vh_from_params;
						vhfiles.Add(vh.vh);
                        vagfiles.Add(vh);
					}

				}
				if (pcandidates.Length > 0)
				{
					plist.Remove(pcandidates[0]);
					pcandidates = plist.ToArray();
				}
				else
                {
                    vh_from_params = false;
                    if (seq_vh_search_all)
                    {
						vhsearch = vh.vh;
					}
                    else
                    {
                        vhsearch = -1;
                    }
					if (allow_vabp && !vabp && vhsearch == -1)
					{
						vabp = true;
						plist = clist.Distinct().ToList();
						pcandidates = plist.ToArray();
						vh_from_params = true;
					}
                    if (strict_size && vhsearch == -1 && vagfiles.Count == 0)
                    {
                        strict_size = false;
						plist = clist.Distinct().ToList();
						pcandidates = plist.ToArray();
						vh_from_params = true;
					}
				}
			}

			if (checkall && !checkbrute)
			{
				List<int> list = new();
				int searchloc = -16;
				do
				{
					searchloc = FindFile(table.ram, "\0\awwwwwwwwwwwwww", searchloc + 16);
					list.Add(searchloc);
				} while (searchloc >= 0);

				searchloc = -16;
				do
				{
					searchloc = FindFile(table.ram, "\a\a\a\a\a\a\a\a\a\a\a\a\a\a\a\a", searchloc + 16);
					list.Add(searchloc);
				} while (searchloc >= 0);


				foreach (int s in list)
				{
					int rloc = s;
					for (int i = 0; i < maxvag; i++)
					{
						if (rloc > 16)
						{
							rloc = FindFile(table.ram, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0", rloc - 16, null, true);
							clist.Add(rloc);
						}
					}
				}
			}
			if (checkbrute)
			{
				clist.Clear();
                param_num = 0;
				int rrloc = -16;
				do
				{
					rrloc = FindFile(table.ram, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0", rrloc + 16);
					clist.Add(rrloc);
                    if (verbose && clist.Count % 1000 == 0)
                    {
                        con.WriteLine("{0} potential VBs found by location {1}/{2}, continuing to check...", clist.Count, rrloc, table.ram.Length);
                    }
				} while (rrloc >= 0);

			}
			int[] candidates = clist.Distinct().ToArray();
            if (verbose)
            {
                con.WriteLine("{0} total VB candidates found", candidates.Length);
            }
			if (!checkbrute)
			{
				for (int i = 0; i < candidates.Length; i++)
				{
					candidates[i] = FindFile(table.ram, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0", candidates[i]);
				}
			}

            List<VabInfo> vi = new();
            foreach (VhInfo k in vagfiles)
            {
                int correct;
                int guess = 0;
                int best = -1;
                int prev_vag;
                int next_vag;
				VabInfo vab = new();
				for (int j = 0; j < candidates.Length; j++)
                {
                    if (verbose && j > 0 && j % 1000 == 0)
                    {
                        if (check_sample_ends)
                        {
							con.WriteLine("Checking VB candidate {0}, best candidate has {1} correct sample starts/ends out of {2}", j, best, k.vagnum * 2);
						}
                        else
                        {
							con.WriteLine("Checking VB candidate {0}, best candidate has {1} correct samples out of {2}", j, best, k.vagnum);
						}
					}
                    if (candidates[j] > 0)
                    {
                        correct = 0;
                        prev_vag = candidates[j];
                        for (int i = 0; i < k.vagnum; i++) 
                        {
                            next_vag = FindFile(table.ram, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0", prev_vag + 16);
                            if (next_vag - prev_vag == k.vags[i])
                            {
                                correct++;
                            }
                            if (check_sample_ends || i == k.vagnum - 1) //do this every time? too slow not worth it
                            {
                                if (table.ram[prev_vag + (k.vags[i] - 15)] == 3 ||
                                FindFile(table.ram, "\0\awwwwwwwwwwwwww", prev_vag + 16) - prev_vag == k.vags[i] ||
                                FindFile(table.ram, "\a\a\a\a\a\a\a\a\a\a\a\a\a\a\a\a", prev_vag + 16) - prev_vag == k.vags[i])
                                {
                                    correct++;
                                }
                            }
                            prev_vag = next_vag;
                        }
                        //ints.Add(correct);
                        if (correct > best)
                        {
                            best = correct;
                            guess = j;
                        }
                    }
                }
                if (guess <= param_num)
                {
                    vab.vb_from_info = true;
                }
                else
                {
                    vab.vb_from_info = false;
                }
                vab.vhstart = k.vh;
                vab.vhend = k.vh + k.vh_size;
                vab.vbstart = candidates[guess];
                vab.vbend = vab.vbstart + k.vag_size;
                vab.vh_from_info = k.vh_from_info;

                int multiplier = 1;

				if (check_sample_ends)
                {
                    multiplier = 2;
				}

				vab.vbprob = (decimal)best / (decimal)(k.vagnum * multiplier);

                if (use_probability)
                {
					if (vab.vbprob < vb_correct_needed)
					{
						vab.vb_not_found = true;
					}
					else
					{
						vab.vb_not_found = false;
					}
				} 
                else if ((k.vagnum * multiplier) - best <= vb_correct_needed)
                {
                    vab.vb_not_found = false;
                }
                else
                {
                    vab.vb_not_found = true;
                }

                vab.md5 = GetMD5(table.ram[vab.vhstart..vab.vhend].Concat(table.ram[vab.vbstart..vab.vbend]).ToArray());


				vi.Add(vab);
            }
            si.vab = vi.ToArray();

            rampar.Dispose();
			return si;
        }

        static SeqInfo[] CountSepTracks(byte[] mem, int index = 0, bool strict = true, int sep_file = -1, bool from_info = false, int main_track = -1)
		{
            //string ram = Encoding.Latin1.GetString(mem);
            int loc = index + 6;
			List<SeqInfo> list = new();


            bool found = true;
            while (found)
            {
                SeqInfo s = new();
                found = false;

				int seqdat = BinaryPrimitives.ReverseEndianness(BitConverter.ToInt32(mem, loc + 9)) + 13;
				loc += seqdat;

				if (seqdat < 0 || loc > mem.Length)
                {
                    break;
                }


				if (mem[loc - 3] == 0xFF && mem[loc - 2] == 0x2F && mem[loc - 1] == 0x00)
                {
                    found = true;
				}
                else if (!strict)
                {
					if (mem[loc - 3] == 0xFF && mem[loc - 2] == 0x00 && mem[loc - 1] == 0x00)
					{
						found = true;
					}
                    else if (BitConverter.ToInt32(mem, loc - 16) == 0 &&
						BitConverter.ToInt32(mem, loc - 12) == 0 &&
						BitConverter.ToInt32(mem, loc - 8) == 0 &&
						BitConverter.ToInt32(mem, loc - 4) == 0)
                    {
                        found = true;
					}
				}

                if ((loc + 13) >= mem.Length)
                {
                    break;
                }

                if (found)
                {
					s.seqstart = loc - seqdat;
					s.seqend = loc - 1;
					s.is_sep = true;
                    s.sep_file = sep_file;
                    s.file_track = list.Count;
                    s.md5 = GetMD5(mem, s.seqstart + 2, s.seqend);
                    s.enabled = false;
                    s.seq_from_info = from_info;
                    s.priority = 0;
                    if (from_info)
                    {
                        s.priority++;
                    }
                    if (s.file_track == main_track)
                    {
                        s.priority += 2;
                    }
					list.Add(s);
				}
            }

            return list.ToArray();
        }
        static int FindFile(byte[] ram, string magic, int start = 0, int[] candidates = null, bool reverse = false)
        {
            if (candidates == null)
            {
                candidates = Array.Empty<int>();
            }
            //StreamReader sr = new StreamReader(ram, System.Text.Encoding.Latin1); //need correct byte length
            string mem = Encoding.Latin1.GetString(ram);
            byte[] bytes = Encoding.Latin1.GetBytes(magic);

            foreach (int candidate in candidates) 
            {
                Array.Copy(ram, candidate, bytes, 0, bytes.Length);
                if (bytes.SequenceEqual(Encoding.Latin1.GetBytes(magic)))
                {
                    return candidate;
                }
            }
            if (reverse)
            {
                return mem.LastIndexOf(magic, start, StringComparison.Ordinal);
            }
            return mem.IndexOf(magic, start, StringComparison.Ordinal);
        }

        static string GetMD5(byte[] ram, int start = 0, int end = -1)
        {
            string m;
            if (end == -1)
            {
                end = ram.Length;
            }
            MD5 md5 = MD5.Create();
            m = Convert.ToHexString(md5.ComputeHash(ram, start, end - start));
            md5.Clear();
            md5.Dispose();
            return m;
        }

        static byte[] RemoveLibTags(byte[] data) 
        {
			List<string> liblines = new();
			StreamReader sr = new(new MemoryStream(data));
            BinaryReader br = new(sr.BaseStream);
            string lib = "";
			uint tagsig = br.ReadUInt32();

			if (tagsig == 0x4741545B && br.ReadByte() == 0x5D)
			{
				while (sr.Peek() >= 0)
				{
					try
					{
						lib = sr.ReadLine();
						if (!lib.ToLowerInvariant().StartsWith("_lib"))
						{
							liblines.Add(lib);
						}
					}
					catch (Exception tx)
					{
						Console.Error.WriteLine("Exception: {0}", tx.Message);
						Console.Error.WriteLine("{0} was not a valid tag line", lib);
					}

				}
			}

            sr.Dispose();
            br.Dispose();

            string tagtext = "[TAG]" + String.Join(Environment.NewLine, liblines);
			return Encoding.UTF8.GetBytes(tagtext);
        }


        static void FindChanges(PsfTable table, uint start, uint end)
        {

            SortedDictionary<uint, bool> layers = new();
            PsfSection[] lChanges = new PsfSection[table.minipsfs.Count * 2];
            PsfSection section = new();
            int j = 0;
            int hLayer = -1;

            for (uint i = 0; i < table.minipsfs.Count; i++)
            {
                section.layer = i;
                section.start = true;
                section.loc = table.minipsfs[(int)i].start;
                lChanges[j] = section;
                j++;
                section.start = false;
                section.loc = table.minipsfs[(int)i].end;
                lChanges[j] = section;
                j++;


                layers.Add(i, false);
            }

            Array.Sort(lChanges, (x, y) => x.loc.CompareTo(y.loc));
            j = 0;


            foreach(PsfSection psf in lChanges)
            {
                j++;
                layers[psf.layer] = psf.start; 
                if (psf.loc >= start && psf.loc <= end)
                {
                    hLayer = -1;
                    for (int i = 0; i < layers.Count; i++)
                    {
                        if (layers[(uint)i] == true)
                        {
                            if (i > hLayer)
                            {
                                hLayer = i;
                            }
                        }
                    }
                    table.minipsfs[hLayer].modified = true;
				}
            }

			return;
        }

        static ParamsV1 DefaultParamsV1(ParamsV1 savepar_test)
        {
			savepar_test.SeqNum = 0;
			savepar_test.Version = 1;
			savepar_test.MvolL = 64;
			savepar_test.MvolR = 64;
			savepar_test.VolL = 64;
			savepar_test.VolR = 64;
			savepar_test.RvolL = 64;
			savepar_test.RvolR = 64;
			savepar_test.RdepthL = 64;
			savepar_test.RdepthR = 64;
			savepar_test.Rdelay = 64;
			savepar_test.Rmode = 3;
			savepar_test.Rfeedback = 64;
			savepar_test.TickMode = 0x1000;
			savepar_test.SeqFlags = (char)0x00;
			savepar_test.SeqType = (char)0x00;
            return savepar_test;
		}

        static bool SaveParamsV1(string arg, ParamsV1 savepar_test)
        {
            try
            {
                FileStream spar = File.OpenWrite(arg);
                BinaryWriter nopointer = new(spar, Encoding.UTF8);
                nopointer.Write(savepar_test.SeqNum);
                nopointer.Write(savepar_test.Version);
                nopointer.Write(savepar_test.MvolL);
                nopointer.Write(savepar_test.MvolR);
                nopointer.Write(savepar_test.VolL);
                nopointer.Write(savepar_test.VolR);
                nopointer.Write(savepar_test.RvolL);
                nopointer.Write(savepar_test.RvolR);
                nopointer.Write(savepar_test.RdepthL);
                nopointer.Write(savepar_test.RdepthR);
                nopointer.Write(savepar_test.Rdelay);
                nopointer.Write(savepar_test.Rmode);
                nopointer.Write(savepar_test.Rfeedback);
                nopointer.Write(savepar_test.TickMode);
                nopointer.Write(savepar_test.SeqFlags);
                nopointer.Write(savepar_test.SeqType);
                nopointer.Close();
                spar.Close();
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("{0} Parameter Write Error: {1}", arg, e.Message);
            }
			return false;
		}

        static string[] SepCatalog(string dir, string ext = "*.exe", TextWriter con = null, SearchOption so = SearchOption.AllDirectories, 
            bool checksep = true, bool printparams = false)
        {
            HashSet<string> sepdirs = new();
            HashSet<string> sepparams = new();
            if (con == null)
            {
                con = Console.Out;
            }
            foreach (string g in Directory.EnumerateFiles(dir, ext, so))
            {
                PsfTable psf = LoadFile(Path.GetFullPath(g));
                bool sep = false;
                if (psf != null)
                {
                    if (checksep)
                    {
                        SoundInfo soundInfo = GetSoundFiles(psf, check_vab: false);
                        if (soundInfo.sep.Length > 0)
                        {
                            sep = true;
                        }
                    }
                    else
                    {
                        sep = true;
                    }
                    if (sep)
                    {
                        sepdirs.Add(Path.GetDirectoryName(g)); //only works when checking seps!
                        InternalParams internalParams = Binvals(new MemoryStream(psf.ram));
                        uint val = 0;
                        if (internalParams != null && internalParams.drivername != null)
                        {
							con.WriteLine("{0}: {1}", g, internalParams.drivername);
						}
                        if (internalParams != null)
                        {
                            foreach (KeyValuePair<string, PsfParameter> pair in internalParams.psfparams)
                            {
                                sepparams.Add(pair.Key);
                                switch (pair.Value.value.Length)
                                {
                                    case 1:
                                        //val = BitConverter.ToChar(pair.Value.value);
                                        val = pair.Value.value[0];
                                        break;
                                    case 2:
                                        val = BitConverter.ToUInt16(pair.Value.value);
                                        break;
                                    case 4:
                                        val = BitConverter.ToUInt32(pair.Value.value);
                                        break;
                                }
                                con.WriteLine("{0} - {1} ({2})", pair.Key, val, pair.Value.value.Length);
                            }
                        }
						Console.WriteLine();
					}
                    //else
                    //{
                        //con.WriteLine("{0} has no SEP files", g);
                    //}
                }
            }

            if (printparams)
            {
                con.WriteLine();
                con.WriteLine("***SEP FILE PARAMETERS***");
                con.WriteLine();
                foreach(string s in sepparams)
                {
                    con.WriteLine(s);
                }
            }
            return sepdirs.ToArray();
        }

        static void ParamsCatalog(string dir, StreamWriter outstream = null, string ext = "*.exe", bool allexe = true, bool sepout = true, bool drvout = true, bool paramout = true)
        {
            //StreamWriter con = new StreamWriter(outstream);
            if (outstream == null)
            {
                outstream = new StreamWriter(Console.OpenStandardOutput());
            }
            Dictionary<string, PsfParameter> psfParameters = new();
            Dictionary<string, InternalParams> psfDrivers = new();
            PsfParameter tp;
            InternalParams td;
            if (allexe)
            {
                DirParams(dir, ext, false, psfParameters, psfDrivers);
            }
            else
            {
                foreach (string d in Directory.EnumerateDirectories(dir))
                {
                    DirParams(Path.GetFullPath(d), ext, true, psfParameters, psfDrivers);
                }
            }
            if (paramout)
            {
                foreach (string k in psfParameters.Keys)
                {
                    if (psfParameters.TryGetValue(k, out tp))
                    {
                        outstream.WriteLine("{0} ({1})", k, tp.value.Length);
                    }

                }
            }
            if (drvout)
            {
                outstream.WriteLine("\n\n\n=====DRIVERS=====\n\n\n");


                foreach (string l in psfDrivers.Keys)
                {
                    if (psfDrivers.TryGetValue(l, out td))
                    {
                        outstream.WriteLine(l);
                        outstream.WriteLine(td.drivername);
                        foreach (string m in td.psfparams.Keys)
                        {
                            if (psfParameters.TryGetValue(m, out tp))
                            {
                                outstream.WriteLine("{0} ({1})", m, tp.value.Length);
                            }

                        }
                    }
                    outstream.WriteLine("\n\n\n");

                }
            }
        }

        static void DirParams(string d, string pattern, bool onefilebreak, Dictionary<string, PsfParameter> parameters, Dictionary<string, InternalParams> drivers)
        {
            PsfTable mem;
            foreach (string f in Directory.EnumerateFiles(d, pattern, SearchOption.AllDirectories))
            {
                //Console.WriteLine(f);
                mem = LoadFile(Path.GetFullPath(f));
                if (mem != null)
                {
                    MemoryStream fstream = new(mem.ram);
                    InternalParams testpar = Binvals(fstream);
                    string drvname;
                    if (testpar != null)
                    {
                        drvname = testpar.drivername;
                        testpar.drivername = Path.GetFullPath(f);
                        drivers.TryAdd(drvname, testpar);
                        foreach (string sp in testpar.psfparams.Keys)
                        {

                            if (testpar.psfparams.TryGetValue(sp, out PsfParameter pp))
                            {
                                parameters.TryAdd(sp, pp);
                            }
                        }

                    }
                    if (onefilebreak)
                    {
                        return;
                    }
                }
            }
        }
        static string Nullterm(string s, int index)
        {
            return s[index..s.IndexOf('\0', index)];
        }

        static string[] psflibs(BinaryReader br, int tagpos) //fix to match with spec - while loop?
        {
            try
            {
                List<string> liblines = new();
                StreamReader sr = new(br.BaseStream);
                string lib = "";
                if (tagpos < 0)
                {
                    br.BaseStream.Seek(4, SeekOrigin.Begin);
                    uint rsize = br.ReadUInt32();
                    uint psize = br.ReadUInt32();
                    tagpos = (int)(16 + psize + rsize);
                }
                if (tagpos > (br.BaseStream.Length - 5))
                {
                    return Array.Empty<string>();
                }
                br.BaseStream.Seek(tagpos, SeekOrigin.Begin);
                uint tagsig = br.ReadUInt32();

                if (tagsig == 0x4741545B && br.ReadByte() == 0x5D)
                {
                    while (sr.Peek() >= 0)
                    {
                        try
                        {
                            lib = sr.ReadLine();
                            if (lib.ToLowerInvariant().StartsWith("_lib"))
                            {
                                liblines.Add(lib);
                            }
                        }
                        catch (Exception tx)
                        {
                            Console.Error.WriteLine("Exception: {0}", tx.Message);
                            Console.Error.WriteLine("{0} was not a valid tag line", lib);
                        }

                    }
                }
                liblines.Sort();
                List<string> libs = new();

                foreach (string ls in liblines)
                {
                    try
                    {
                        libs.Add(ls.Split('=', StringSplitOptions.RemoveEmptyEntries)[1]);
                    }
                    catch (Exception lx)
                    {
                        Console.Error.WriteLine("{0} was not a valid library", ls);
                        Console.Error.WriteLine("Exception: {0}", lx.Message);
                    }
                }
                return libs.ToArray();
            }
            catch (Exception mx)
            {
                Console.Error.WriteLine("Tag Exception: {0}", mx.Message);
            }
            return Array.Empty<string>();
        }

        static PsfTable LoadFile(string filename)
        {
			PsfTable pt = null;
			try
            {
                FileStream fs = new(filename, FileMode.Open);
                BinaryReader br = new(fs);
                uint ftype = br.ReadUInt32();
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
                            pt.minipsfs[0].filename = filename;
                            fs.Dispose();
                        }

                        break;
                    case 0x582D5350: //PSX EXE
                        pt = LoadExe(br);
                        pt.minipsfs = new List<PsfFile>();
                        fs.Dispose();
                        PsfFile info = new()
                        {
                            filename = filename,
                            headersect = new byte[2048]
                        };
                        Array.Copy(pt.ram, info.headersect, 2048);
                        info.segment = BitConverter.ToUInt32(info.headersect, 24) / 0x20000000;
                        info.start = 2048;
                        info.end = (uint)pt.ram.Length;
                        info.modified = false;
                        pt.minipsfs.Add(info);
                        break;
                    default:
                        Console.Error.WriteLine("{0} is not a readable PSF or EXE file!", filename);
                        break;

                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Load Exception: {0}", e.ToString());
            }
            return pt;
        }

        

        static PsfTable LoadMiniPsf(string filename)
        {
			PsfTable ptab = new()
			{
				ftype = PsfTypes.MINIPSF,
				ram = new byte[0x200000],
				minipsfs = new List<PsfFile>()
			};
			LoadPsfFile(filename, ptab);
            uint lowest = uint.MaxValue;
            uint highest = uint.MinValue;
            foreach (PsfFile se in ptab.minipsfs)
            {
                if (se.start < lowest)
                {
                    lowest = se.start;
                }
                if (se.end > highest)
                {
                    highest = se.end;
                }
            }
            
            byte[] mem = new byte[highest - lowest + 2048];
            Array.Copy(ptab.ram, lowest, mem, 2048, highest - lowest);
            Array.Copy(ptab.minipsfs[0].headersect, mem, 2048);
            byte[] size = BitConverter.GetBytes(highest - lowest + ((highest - lowest) % 2048)); //dont know if it cares about this but nocash specs say modulo is needed 
            byte[] start = BitConverter.GetBytes(lowest + (ptab.minipsfs[0].segment * 0x20000000));
            Array.Copy(start, 0, mem, 0x18, 4);
            Array.Copy(size, 0, mem, 0x1C, 4);
            foreach (PsfFile psf in ptab.minipsfs)
            {
                psf.start -= lowest - 2048;
            }
            ptab.ram = mem;
            return ptab;
        }

        static bool LoadPsfFile (string fn, PsfTable tab)
        {
            try
            {
                FileStream file = new(fn, FileMode.Open);
                BinaryReader binary = new(file);
                PsfFile info = new();
                byte[] tempram = new byte[0x200000];
                binary.BaseStream.Seek(4, SeekOrigin.Begin);
                int rsize = binary.ReadInt32();
                int psize = binary.ReadInt32();

                string[] libraries = psflibs(binary, 16 + psize + rsize);
                foreach (string l in libraries)
                {
                    LoadPsfFile(l, tab);
                }

                binary.BaseStream.Seek(12, SeekOrigin.Begin);
                info.crc = binary.ReadUInt32();
                info.modified = false;
                info.reserved_area = binary.ReadBytes(rsize);
                ZlibStream zlib = new ZlibStream(binary.BaseStream, CompressionMode.Decompress);
                int bytesread = zlib.Read(tempram, 0, 0x200000);

                binary.BaseStream.Seek(16 + psize + rsize, SeekOrigin.Begin);
				info.tags = binary.ReadBytes((int)binary.BaseStream.Length - (int)binary.BaseStream.Position);
				info.headersect = new byte[2048];
                Array.Copy(tempram, info.headersect, 2048);

                info.segment = BitConverter.ToUInt32(info.headersect, 24) / 0x20000000;
                info.start = BitConverter.ToUInt32(info.headersect, 24) % 0x20000000;
                info.end = info.start + (uint)(bytesread - 2048);

                Array.Copy(tempram, 2048, tab.ram, info.start, bytesread - 2048);
                binary.BaseStream.Seek(16 + rsize, SeekOrigin.Begin);
                tempram = binary.ReadBytes(psize);
                if (Crc32Algorithm.Compute(tempram) != info.crc)
                {
                    Console.Error.WriteLine("{0}: Wrong CRC!", fn);
                }
                info.filename = fn;
                tab.minipsfs.Add(info);
                zlib.Close();
                binary.Dispose();
                file.Dispose();
                zlib.Dispose();
                return true;
            }
            catch (Exception px)
            {
                Console.Error.WriteLine("File {0} exception: {1}", fn, px.Message);

            }
            return false;
        }

        static bool SaveMiniPSF(string[] fn, PsfTable psfTable, bool cleanlibs = true, int pn = -1)
        {
            if (pn == -1)
            {
                pn = psfTable.minipsfs.Count - 1;
            }

            List<bool> oldModified = new();
            uint oldstart = psfTable.minipsfs[pn].start, oldend = psfTable.minipsfs[pn].end;
            int oldlen = fn.Length;
            bool resized = false;
            byte[] oldtags = null;
            byte[] oldheader = new byte[2048];
            int i = 0;

            if (fn.Length < psfTable.minipsfs.Count)
            {
                resized = true;
				Array.Resize(ref fn, psfTable.minipsfs.Count);
			}

            if (psfTable.ftype == PsfTypes.PSF)
            {
                foreach(PsfFile b in psfTable.minipsfs)
                {
                    oldModified.Add(b.modified);
                    b.modified = false;
                }
				Array.Copy(psfTable.minipsfs[pn].headersect, oldheader, 2048);

				if (cleanlibs)
                {
					oldtags = new byte[psfTable.minipsfs[pn].tags.Length];
					Array.Copy(psfTable.minipsfs[pn].tags, oldtags, oldtags.Length);
					psfTable.minipsfs[pn].tags = RemoveLibTags(oldtags);
				}

                Array.Copy(psfTable.ram, psfTable.minipsfs[pn].headersect, 2048);
				psfTable.minipsfs[pn].modified = true;
                psfTable.minipsfs[pn].start = 2048;
                psfTable.minipsfs[pn].end = (uint)psfTable.ram.Length;

            }
            
            foreach (PsfFile b in psfTable.minipsfs)
            {
                if (b.modified) {
                    SavePsfFile(psfTable.ram, b, fn[i]);
                    i++;
				}
            }

            if (resized)
            {
				Array.Resize(ref fn, oldlen);
			}

            if (psfTable.ftype == PsfTypes.PSF)
            {
                i = 0;
                foreach (bool b in oldModified)
                {
                    psfTable.minipsfs[i].modified = b;
                    i++;
                }
                if (cleanlibs)
                {
                    psfTable.minipsfs[pn].tags = oldtags;
                }
                psfTable.minipsfs[pn].start = oldstart;
                psfTable.minipsfs[pn].end = oldend;
                psfTable.minipsfs[pn].headersect = oldheader;

            }
            return true;
        }

        static bool SavePsfFile(byte[] ram, PsfFile psfFile, string fn = null)
        {
            try
            {
                psfFile.reserved_area ??= Array.Empty<byte>();
				psfFile.tags ??= Array.Empty<byte>();
                if (String.IsNullOrEmpty(fn))
                {
                    fn = psfFile.filename;
                }
				BinaryWriter bw = new(new FileStream(fn, FileMode.Create));
                bw.Write(0x01465350); //PSF signature
                bw.Write(psfFile.reserved_area.Length);

                MemoryStream mem = new();
				ZlibStream zlib = new(mem, CompressionMode.Compress, CompressionLevel.Level9, true);
				zlib.Write(psfFile.headersect);
                uint unc_size = psfFile.end - psfFile.start;
                zlib.Write(ram, (int)psfFile.start, (int)unc_size);
                //zlib.Write(ram);
                zlib.Flush();
                zlib.Close(); //Must use this method even though Micrsoft says it's deprecated
                byte[] tempram = mem.ToArray();

				bw.Write(tempram.Length);
				bw.Write(Crc32Algorithm.Compute(tempram));
                bw.Write(psfFile.reserved_area);
                bw.Write(tempram);

                zlib.Dispose();
                mem.Dispose();

                //int csize = (int)bw.BaseStream.Length - (psfFile.reserved_area.Length + 16);
                bw.Write(psfFile.tags);
                bw.Flush();
                bw.Dispose();

                return true;

			}
            catch (Exception bx)
            {
                Console.Error.WriteLine("File Exception: {0}", bx.Message);
                return false;
            }
        }

        static string FindName(PsfFile psfFile)
        {
            try
            {
                if (psfFile.tags == null || psfFile.tags.Length < 5)
                {
                    return Path.GetFileNameWithoutExtension(psfFile.filename);
                }
                BinaryReader br = new(new MemoryStream(psfFile.tags));
                StreamReader sr = new(br.BaseStream);
                uint tagsig = br.ReadUInt32();
                string lib = "";
                string fname = null;
                if (tagsig == 0x4741545B && br.ReadByte() == 0x5D)
                {
                    while (sr.Peek() >= 0)
                    {
                        try
                        {
                            lib = sr.ReadLine();
                            if (lib.ToLowerInvariant().StartsWith("title"))
                            {
                                fname = (lib.Split('=', StringSplitOptions.RemoveEmptyEntries)[1]);
                            }
                        }
                        catch (Exception tx)
                        {
                            Console.Error.WriteLine("Exception: {0}", tx.Message);
                            Console.Error.WriteLine("{0} was not a valid tag line", lib);
                        }

                    }
                }
                if (fname == null || fname.Length == 0)
                {
                    return Path.GetFileNameWithoutExtension(psfFile.filename); //if both are null returns null
                }
                else
                {
                    return fname;
                }
            } 
            catch (Exception cx)
            {
                Console.Error.WriteLine("Tag Field Exception: {0}", cx.Message);
                return null;
            }
        }

        static bool SaveEXEFile(string fn, PsfTable psfTable)
        {
            try
            {
                BinaryWriter binaryWriter = new(File.OpenWrite(fn));
                binaryWriter.Write(psfTable.ram);
                binaryWriter.Close();
                return true;
            }
            catch (Exception vx)
            {
                Console.Error.WriteLine("EXE Save Error {0} for file {1}", vx.Message, fn);
            }
            return false;
        }
        static PsfTable LoadPsf(BinaryReader f)
        {
            PsfTable ptab = new();
            ptab.ftype = PsfTypes.PSF;
            ptab.minipsfs = new List<PsfFile>();
            PsfFile info = new();
            byte[] tempram = new byte[0x200000];
            f.BaseStream.Seek(4, SeekOrigin.Begin);
            int rsize = f.ReadInt32();
            int psize = f.ReadInt32();
            info.crc = f.ReadUInt32();
            info.modified = false;
            info.reserved_area = f.ReadBytes(rsize);
            ZlibStream zlib = new(f.BaseStream, CompressionMode.Decompress);
            int bytesread = zlib.Read(tempram, 0, 0x200000);
            info.headersect = new byte[2048];
            Array.Copy(tempram, info.headersect, 2048);
            ptab.ram = new byte[bytesread];
            Array.Copy(tempram, ptab.ram, bytesread);
            f.BaseStream.Seek(16 + psize + rsize, SeekOrigin.Begin);
            info.tags = f.ReadBytes((int)f.BaseStream.Length - (int)f.BaseStream.Position);
            f.BaseStream.Seek(16 + rsize, SeekOrigin.Begin);
            tempram = f.ReadBytes(psize);
            if (Crc32Algorithm.Compute(tempram) != info.crc)
            {
                Console.Error.WriteLine("Wrong CRC!");
            }
            info.segment = BitConverter.ToUInt32(info.headersect, 24) / 0x20000000;
            info.start = 2048;
            info.end = (uint)(bytesread - 2048) + info.start;
            ptab.minipsfs.Add(info);
            return ptab;
        }

        static PsfTable LoadExe(BinaryReader b)
        {
            b.BaseStream.Seek(0, SeekOrigin.Begin);
            PsfTable psf = new();
            psf.ftype = PsfTypes.EXE;
            psf.ram = b.ReadBytes((int)b.BaseStream.Length);
            return psf;
        }
        static InternalParams Binvals(Stream fs) //rewrite this without streams if you ever get around to it
        {// assuming now that the segment is consistent
            try
            {
                uint param, param2, ipoffset, tparam, tparam2;
                int param3;
                long postemp;
                
                BinaryReader br = new(fs);
                StreamReader sr = new(fs, Encoding.ASCII);
                string psfexe = sr.ReadToEnd();
				InternalParams ip = new()
				{
					sig = psfexe.IndexOf("PSF_DRIVER_INFO:")
				};
				fs.Seek(24, SeekOrigin.Begin);
                ip.offset = br.ReadUInt32() - 2048;
                ipoffset = ip.offset % 0x20000000;
                fs.Seek(ip.sig + 16, SeekOrigin.Begin);
                ip.loadaddr = br.ReadUInt32();
                ip.entrypoint = br.ReadUInt32();
                ip.drivernameloc = br.ReadUInt32();
                ip.drivername = Nullterm(psfexe, (int)(ip.drivernameloc % 0x20000000 - ipoffset));
                ip.exenameloc = br.ReadUInt32();
                ip.exename = Nullterm(psfexe, (int)(ip.exenameloc % 0x20000000 - ipoffset));
                ip.crc = br.ReadUInt32();
                ip.jumppatch = br.ReadUInt32();
                SongArea sa;
                param = br.ReadUInt32();
                param2 = br.ReadUInt32();
                sa.addr = param;
                sa.size = param2;
                ip.blocks = new List<SongArea>();
                while (param != 0 && param2 != 0)
                {
                    ip.blocks.Add(sa);
                    param = br.ReadUInt32();
                    param2 = br.ReadUInt32();
                    sa.addr = param;
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
                while (tparam != 0 && tparam2 != 0) // && param3 != 0
				{ //potential 0 byte parameters as comments or something. but there could be a 0 length parameter as end
                    fs.Seek(param2, SeekOrigin.Begin);
                    PsfParameter pp = new(tparam2, tparam, br.ReadBytes(param3));
                    ip.psfparams.Add(Nullterm(psfexe, (int)param), pp);
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
                Console.Error.WriteLine("PSF parameter block read error: {0}", ex.Message);
            }
            return null;
        }

        static byte[] BinaryDriverInfo(InternalParams ip) //parameters to be loaded from json for notepad
        { //no adding parameters or changing names since the exe would have to be recompiled for it to do anything, and string table has to get moved
            MemoryStream memory = new();
            BinaryWriter bw = new(memory);
            StreamWriter sw = new(memory, Encoding.ASCII);
            //uint ipoffset = (ip.offset % 0x20000000) + (segment * 0x20000000);


			sw.Write("PSF_DRIVER_INFO:");
            sw.Flush();
            bw.Write(ip.loadaddr);
            bw.Write(ip.entrypoint);
            bw.Write(ip.drivernameloc);
            bw.Write(ip.exenameloc);
            bw.Write(ip.crc);
            bw.Write(ip.jumppatch);
            foreach(SongArea song in ip.blocks)
            {
                bw.Write(song.addr);
                bw.Write(song.size);
            }
			bw.Write(0x00000000);
			foreach (KeyValuePair<string,PsfParameter> p in ip.psfparams)
            {
                bw.Write(p.Value.nameloc);
                bw.Write(p.Value.loc);
                bw.Write(p.Value.value.Length);
            }
            bw.Write(0x00000000);
            bw.Flush();
            byte[] r = memory.ToArray();
            sw.Dispose();
            bw.Dispose();
            memory.Dispose();
            return r;
        }
		static bool FixParameter(PsfParameter param, uint offset, byte[] ram)
		{
            try
            {
                Array.Copy(param.value, 0, ram, param.loc - offset, param.value.Length);
            }
            catch (Exception ex) { 
                Console.Error.WriteLine("Parameter Edit Error: {0}", ex.Message);
            }
			return true;
		}
	}
}
