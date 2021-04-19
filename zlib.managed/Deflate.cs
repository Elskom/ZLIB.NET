// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;

    internal sealed class Deflate
    {
        internal Deflate()
        {
            this.DynLtree = new short[1146];
            this.DynDtree = new short[122]; // distance tree
            this.BlTree = new short[78]; // Huffman tree for bit lengths
        }

        internal byte[] PendingBuf { get; private set; } // output still pending

        internal int PendingOut { get; set; } // next pending byte to output to the stream

        internal int Pending { get; set; } // nb of bytes in the pending buffer

        internal int Noheader { get; private set; } // suppress zlib header and adler32

        // number of codes at each bit length for an optimal tree
        internal short[] BlCount { get; } = new short[16];

        // heap used to build the Huffman trees
        internal int[] Heap { get; } = new int[573];

        internal int HeapLen { get; set; } // number of elements in the heap

        internal int HeapMax { get; set; } // element of largest frequency

        // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
        // The same heap array is used to build all trees.

        // Depth of each subtree used as tie breaker for trees of equal frequency
        internal byte[] Depth { get; } = new byte[573];

        internal int LastLit { get; private set; } // running index in l_buf

        internal int OptLen { get; set; } // bit length of current block with optimal trees

        internal int StaticLen { get; set; } // bit length of current block with static trees

        private static ReadOnlySpan<Config> ConfigTable => new[]
        {
            // good  lazy  nice  chain
            new Config(0, 0, 0, 0, 0/*stored*/), // 0
            new Config(4, 4, 8, 4, 1/*fast*/),  // 1
            new Config(4, 5, 16, 8, 1/*fast*/),  // 2
            new Config(4, 6, 32, 32, 1/*fast*/),  // 3
            new Config(4, 4, 16, 16, 2/*slow*/),  // 4
            new Config(8, 16, 32, 32, 2/*slow*/),  // 5
            new Config(8, 16, 128, 128, 2/*slow*/),  // 6
            new Config(8, 32, 128, 256, 2/*slow*/),  // 7
            new Config(32, 128, 258, 1024, 2/*slow*/),  // 8
            new Config(32, 258, 258, 4096, 2/*slow*/),  // 9
        };

        private ZlibStream Strm { get; set; } // pointer back to this zlib stream

        private int Status { get; set; } // as the name implies

        private int PendingBufSize { get; set; } // size of pending_buf

        private byte DataType { get; set; } // UNKNOWN, BINARY or ASCII

        private byte Method { get; set; } // STORED (for zip only) or DEFLATED

        private ZlibFlushStrategy LastFlush { get; set; } // value of flush param for previous deflate call

        private int WSize { get; set; } // LZ77 window size (32K by default)

        private int WBits { get; set; } // log2(w_size)  (8..16)

        private int WMask { get; set; } // w_size - 1

        private byte[] Window { get; set; }

        // Sliding window. Input bytes are read into the second half of the window,
        // and move to the first half later to keep a dictionary of at least wSize
        // bytes. With this organization, matches are limited to a distance of
        // wSize-MAX_MATCH bytes, but this ensures that IO is always
        // performed with a length multiple of the block size. Also, it limits
        // the window size to 64K, which is quite useful on MSDOS.
        // To do: use the user input buffer as sliding window.
        private int WindowSize { get; set; }

        // Actual size of window: 2*wSize, except when the user input buffer
        // is directly used as sliding window.
        private short[] Prev { get; set; }

        // Link to older string with same hash index. To limit the size of this
        // array to 64K, this link is maintained only for the last 32K strings.
        // An index in this array is thus a window index modulo 32K.
        private short[] Head { get; set; } // Heads of the hash chains or NIL.

        private int InsH { get; set; } // hash index of string to be inserted

        private int HashSize { get; set; } // number of elements in hash table

        private int HashBits { get; set; } // log2(hash_size)

        private int HashMask { get; set; } // hash_size-1

        // Number of bits by which ins_h must be shifted at each input
        // step. It must be such that after MIN_MATCH steps, the oldest
        // byte no longer takes part in the hash key, that is:
        // hash_shift * MIN_MATCH >= hash_bits
        private int HashShift { get; set; }

        // Window position at the beginning of the current output block. Gets
        // negative when the window is moved backwards.
        private int BlockStart { get; set; }

        private int MatchLength { get; set; } // length of best match

        private int PrevMatch { get; set; } // previous match

        private int MatchAvailable { get; set; } // set if previous match exists

        private int Strstart { get; set; } // start of string to insert

        private int MatchStart { get; set; } // start of matching string

        private int Lookahead { get; set; } // number of valid bytes ahead in window

        // Length of the best match at previous step. Matches not greater than this
        // are discarded. This is used in the lazy match evaluation.
        private int PrevLength { get; set; }

        // To speed up deflation, hash chains are never searched beyond this
        // length.  A higher limit improves compression ratio but degrades the speed.
        private int MaxChainLength { get; set; }

        // Attempt to find a better match only when the current match is strictly
        // smaller than this value. This mechanism is used only for compression
        // levels >= 4.
        private int MaxLazyMatch { get; set; }

        // Insert new strings in the hash table only if the match length is not
        // greater than this length. This saves time but degrades compression.
        // max_insert_length is used only for compression levels <= 3.
        private ZlibCompression Level { get; set; } // compression level (1..9)

        private ZlibCompressionStrategy Strategy { get; set; } // favor or force Huffman coding

        // Use a faster search when the previous match is longer than this
        private int GoodMatch { get; set; }

        // Stop searching when current match exceeds this
        private int NiceMatch { get; set; }

        private short[] DynLtree { get; } // literal and length tree

        private short[] DynDtree { get; } // distance tree

        private short[] BlTree { get; } // Huffman tree for bit lengths

        private Tree LDesc { get; } = new(); // desc for literal tree

        private Tree DDesc { get; } = new(); // desc for distance tree

        private Tree BlDesc { get; } = new(); // desc for bit length tree

        private int LBuf { get; set; } // index for literals or lengths */

        // Size of match buffer for literals/lengths.  There are 4 reasons for
        // limiting lit_bufsize to 64K:
        //   - frequencies can be kept in 16 bit counters
        //   - if compression is not successful for the first block, all input
        //     data is still in the window so we can still emit a stored block even
        //     when input comes from standard input.  (This can also be done for
        //     all blocks if lit_bufsize is not greater than 32K.)
        //   - if compression is not successful for a file smaller than 64K, we can
        //     even emit a stored file instead of a stored block (saving 5 bytes).
        //     This is applicable only for zip (not gzip or zlib).
        //   - creating new Huffman trees less frequently may not provide fast
        //     adaptation to changes in the input data statistics. (Take for
        //     example a binary file with poorly compressible code followed by
        //     a highly compressible string table.) Smaller buffer sizes give
        //     fast adaptation but have of course the overhead of transmitting
        //     trees more frequently.
        //   - I can't count above 4
        private int LitBufsize { get; set; }

        // Buffer for distances. To simplify the code, d_buf and l_buf have
        // the same number of elements. To use different lengths, an extra flag
        // array would be necessary.
        private int DBuf { get; set; } // index of pendig_buf

        private int Matches { get; set; } // number of string matches in current block

        private int LastEobLen { get; set; } // bit length of EOB code for last block

        // Output buffer. bits are inserted starting at the bottom (least
        // significant bits).
        private short BiBuf { get; set; }

        // Number of valid bits in bi_buf.  All bits above the last valid bit
        // are always zero.
        private int BiValid { get; set; }

        // Restore the heap property by moving down the tree starting at node k,
        // exchanging a node with the smallest of its two sons if necessary, stopping
        // when the heap property is re-established (each father smaller than its
        // two sons).
        internal void Pqdownheap(short[] tree, int k)
        {
            var v = this.Heap[k];
            var j = k << 1; // left son of k
            while (j <= this.HeapLen)
            {
                // Set j to the smallest of the two sons:
                if (j < this.HeapLen && Smaller(tree, this.Heap[j + 1], this.Heap[j], this.Depth))
                {
                    j++;
                }

                // Exit if v is smaller than both sons
                if (Smaller(tree, v, this.Heap[j], this.Depth))
                {
                    break;
                }

                // Exchange v with the smallest son
                this.Heap[k] = this.Heap[j];
                k = j;

                // And continue down the tree, setting j to the left son of k
                j <<= 1;
            }

            this.Heap[k] = v;
        }

        internal ZlibCompressionState DeflateInit(ZlibStream stream, ZlibCompression level, int bits)
            => this.DeflateInit2(stream, level, 8, bits, 8, ZlibCompressionStrategy.DefaultStrategy);

        internal ZlibCompressionState DeflateEnd()
        {
            if (this.Status is not 42 and not 113 and not 666)
            {
                return ZlibCompressionState.StreamError;
            }

            // Deallocate in reverse order of allocations:
            this.PendingBuf = null;
            this.Head = null;
            this.Prev = null;
            this.Window = null;

            // free
            return this.Status is 113 ? ZlibCompressionState.DataError : ZlibCompressionState.Ok;
        }

        internal ZlibCompressionState Compress(ZlibStream stream, ZlibFlushStrategy flush)
        {
            if (flush is > ZlibFlushStrategy.Finish or < 0)
            {
                return ZlibCompressionState.StreamError;
            }

            if (stream.NextOut is null || (stream.NextIn is null && stream.AvailIn is not 0) || (this.Status is 666 && flush is not ZlibFlushStrategy.Finish))
            {
                stream.Msg = "stream error";
                return ZlibCompressionState.StreamError;
            }

            if (stream.AvailOut is 0)
            {
                stream.Msg = "buffer error";
                return ZlibCompressionState.BufError;
            }

            this.Strm = stream; // just in case
            var oldFlush = this.LastFlush;
            this.LastFlush = flush;

            // Write the zlib header
            if (this.Status is 42)
            {
                var header = (8 + ((this.WBits - 8) << 4)) << 8;
                var levelFlags = (((int)this.Level - 1) & 0xff) >> 1;
                if (levelFlags > 3)
                {
                    levelFlags = 3;
                }

                header |= levelFlags << 6;
                if (this.Strstart is not 0)
                {
                    header |= 32;
                }

                header += 31 - (header % 31);
                this.Status = 113;
                this.PutShortMsb(header);

                // Save the adler32 of the preset dictionary:
                if (this.Strstart is not 0)
                {
                    this.PutShortMsb((int)(stream.Adler >= 0 ? stream.Adler >> 16 : (stream.Adler >> 16) + 281474976710656L));
                    this.PutShortMsb((int)(stream.Adler & 0xffff));
                }

                stream.Adler = Adler32.Calculate(0, null, 0, 0);
            }

            // Flush as much pending output as possible
            if (this.Pending is not 0)
            {
                stream.Flush_pending();
                if (stream.AvailOut is 0)
                {
                    // Since avail_out is 0, deflate will be called again with
                    // more output space, but possibly with both pending and
                    // avail_in equal to zero. There won't be anything to do,
                    // but this is not an error situation so make sure we
                    // return OK instead of BUF_ERROR at next call of deflate:
                    this.LastFlush = (ZlibFlushStrategy)(-1);
                    return ZlibCompressionState.Ok;
                }

                // Make sure there is something to do and avoid duplicate consecutive
                // flushes. For repeated and useless calls with Z_FINISH, we keep
                // returning Z_STREAM_END instead of Z_BUFF_ERROR.
            }
            else if (stream.AvailIn is 0 && flush <= oldFlush && flush is not ZlibFlushStrategy.Finish)
            {
                stream.Msg = "buffer error";
                return ZlibCompressionState.BufError;
            }

            // User must not provide more input after the first FINISH:
            if (this.Status is 666 && stream.AvailIn is not 0)
            {
                stream.Msg = "buffer error";
                return ZlibCompressionState.BufError;
            }

            // Start a new block or continue the current one.
            if (stream.AvailIn is not 0 || this.Lookahead is not 0 || (flush is not ZlibFlushStrategy.NoFlush && this.Status is not 666))
            {
                var bstate = ConfigTable[(int)this.Level].Func switch
                {
                    0 => this.Deflate_stored(flush),
                    1 => this.Deflate_fast(flush),
                    2 => this.Deflate_slow(flush),
                    _ => throw new NotSupportedException(),
                };

                if (bstate is 2 or 3)
                {
                    this.Status = 666;
                }

                switch (bstate)
                {
                    case 0 or 2:
                    {
                        if (stream.AvailOut is 0)
                        {
                            this.LastFlush = (ZlibFlushStrategy)(-1); // avoid BUF_ERROR next call, see above
                        }

                        return ZlibCompressionState.Ok;

                        // If flush != Z_NO_FLUSH && avail_out == 0, the next call
                        // of deflate should use the same flush parameter to make sure
                        // that the flush is complete. So we don't have to output an
                        // empty block here, this will be done at next call. This also
                        // ensures that for a very small output buffer, we emit at most
                        // one empty block.
                    }

                    case 1:
                    {
                        if (flush is ZlibFlushStrategy.PartialFlush)
                        {
                            this.Tr_align();
                        }
                        else
                        {
                            // FULL_FLUSH or SYNC_FLUSH
                            this.Tr_stored_block(0, 0, false);

                            // For a full flush, this empty block will be recognized
                            // as a special marker by inflate_sync().
                            if (flush is ZlibFlushStrategy.FullFlush)
                            {
                                for (var i = 0; i < this.HashSize; i++)
                                {
                                    // forget history
                                    this.Head[i] = 0;
                                }
                            }
                        }

                        stream.Flush_pending();
                        if (stream.AvailOut is 0)
                        {
                            this.LastFlush = (ZlibFlushStrategy)(-1); // avoid BUF_ERROR at next call, see above
                            return ZlibCompressionState.Ok;
                        }

                        break;
                    }

                    default:
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            if (flush is not ZlibFlushStrategy.Finish)
            {
                return ZlibCompressionState.Ok;
            }

            if (this.Noheader is not 0)
            {
                return ZlibCompressionState.StreamEnd;
            }

            // Write the zlib trailer (adler32)
            this.PutShortMsb((int)(stream.Adler >= 0 ? stream.Adler >> 16 : (stream.Adler >> 16) + 281474976710656L));
            this.PutShortMsb((int)(stream.Adler & 0xffff));
            stream.Flush_pending();

            // If avail_out is zero, the application will call deflate again
            // to flush the rest.
            this.Noheader = -1; // write the trailer only once!
            return this.Pending is not 0 ? ZlibCompressionState.Ok : ZlibCompressionState.StreamEnd;
        }

        private static bool Smaller(short[] tree, int n, int m, byte[] depth)
            => tree[n * 2] < tree[m * 2] || (tree[n * 2] == tree[m * 2] && depth[n] <= depth[m]);

        private void Lm_init()
        {
            this.WindowSize = 2 * this.WSize;
            this.Head[this.HashSize - 1] = 0;
            for (var i = 0; i < this.HashSize - 1; i++)
            {
                this.Head[i] = 0;
            }

            // Set the default configuration parameters:
            this.MaxLazyMatch = ConfigTable[(int)this.Level].MaxLazy;
            this.GoodMatch = ConfigTable[(int)this.Level].GoodLength;
            this.NiceMatch = ConfigTable[(int)this.Level].NiceLength;
            this.MaxChainLength = ConfigTable[(int)this.Level].MaxChain;
            this.Strstart = 0;
            this.BlockStart = 0;
            this.Lookahead = 0;
            this.MatchLength = this.PrevLength = 2;
            this.MatchAvailable = 0;
            this.InsH = 0;
        }

        // Initialize the tree data structures for a new zlib stream.
        private void Tr_init()
        {
            this.LDesc.DynTree = this.DynLtree;
            this.LDesc.StatDesc = StaticTree.StaticLDesc;
            this.DDesc.DynTree = this.DynDtree;
            this.DDesc.StatDesc = StaticTree.StaticDDesc;
            this.BlDesc.DynTree = this.BlTree;
            this.BlDesc.StatDesc = StaticTree.StaticBlDesc;
            this.BiBuf = 0;
            this.BiValid = 0;
            this.LastEobLen = 8; // enough lookahead for inflate

            // Initialize the first block of the first file:
            this.Init_block();
        }

        private void Init_block()
        {
            // Initialize the trees.
            for (var i = 0; i < 286; i++)
            {
                this.DynLtree[i * 2] = 0;
            }

            for (var i = 0; i < 30; i++)
            {
                this.DynDtree[i * 2] = 0;
            }

            for (var i = 0; i < 19; i++)
            {
                this.BlTree[i * 2] = 0;
            }

            this.DynLtree[512] = 1;
            this.OptLen = this.StaticLen = 0;
            this.LastLit = this.Matches = 0;
        }

        // Scan a literal or distance tree to determine the frequencies of the codes
        // in the bit length tree.
        private void Scan_tree(short[] tree, int maxCode)
        {
            int n; // iterates over all tree elements
            var prevlen = -1; // last emitted length
            int nextlen = tree[(0 * 2) + 1]; // length of next code
            var count = 0; // repeat count of the current code
            var maxCount = 7; // max repeat count
            var minCount = 4; // min repeat count
            if (nextlen is 0)
            {
                maxCount = 138;
                minCount = 3;
            }

            tree[((maxCode + 1) * 2) + 1] = unchecked((short)0xffffL); // guard
            for (n = 0; n <= maxCode; n++)
            {
                var curlen = nextlen; // length of current code
                nextlen = tree[((n + 1) * 2) + 1];
                if (++count < maxCount && curlen == nextlen)
                {
                    continue;
                }
                else if (count < minCount)
                {
                    this.BlTree[curlen * 2] = (short)(this.BlTree[curlen * 2] + count);
                }
                else if (curlen != 0)
                {
                    if (curlen != prevlen)
                    {
                        this.BlTree[curlen * 2]++;
                    }

                    this.BlTree[32]++;
                }
                else if (count <= 10)
                {
                    this.BlTree[34]++;
                }
                else
                {
                    this.BlTree[36]++;
                }

                count = 0;
                prevlen = curlen;
                maxCount = nextlen is 0 ? 138 : curlen == nextlen ? 6 : 7;
                minCount = nextlen is 0 || curlen == nextlen ? 3 : 4;
            }
        }

        // Construct the Huffman tree for the bit lengths and return the index in
        // bl_order of the last bit length code to send.
        private int Build_bl_tree()
        {
            int maxBlindex; // index of last bit length code of non zero freq

            // Determine the bit length frequencies for literal and distance trees
            this.Scan_tree(this.DynLtree, this.LDesc.MaxCode);
            this.Scan_tree(this.DynDtree, this.DDesc.MaxCode);

            // Build the bit length tree:
            this.BlDesc.Build_tree(this);

            // opt_len now includes the length of the tree representations, except
            // the lengths of the bit lengths codes and the 5+5+4 bits for the counts.

            // Determine the number of bit length codes to send. The pkzip format
            // requires that at least 4 bit length codes be sent. (appnote.txt says
            // 3 but the actual value used is 4.)
            for (maxBlindex = 18; maxBlindex >= 3; maxBlindex--)
            {
                if (this.BlTree[(Tree.BlOrder[maxBlindex] * 2) + 1] != 0)
                {
                    break;
                }
            }

            // Update opt_len to include the bit length tree and counts
            this.OptLen += (3 * (maxBlindex + 1)) + 5 + 5 + 4;
            return maxBlindex;
        }

        // Send the header for a block using dynamic Huffman trees: the counts, the
        // lengths of the bit length codes, the literal tree and the distance tree.
        // IN assertion: lcodes >= 257, dcodes >= 1, blcodes >= 4.
        private void Send_all_trees(int lcodes, int dcodes, int blcodes)
        {
            int rank; // index in bl_order
            this.Send_bits(lcodes - 257, 5); // not +255 as stated in appnote.txt
            this.Send_bits(dcodes - 1, 5);
            this.Send_bits(blcodes - 4, 4); // not -3 as stated in appnote.txt
            for (rank = 0; rank < blcodes; rank++)
            {
                this.Send_bits(this.BlTree[(Tree.BlOrder[rank] * 2) + 1], 3);
            }

            this.Send_tree(this.DynLtree, lcodes - 1); // literal tree
            this.Send_tree(this.DynDtree, dcodes - 1); // distance tree
        }

        // Send a literal or distance tree in compressed form, using the codes in
        // bl_tree.
        private void Send_tree(short[] tree, int maxCode)
        {
            int n; // iterates over all tree elements
            var prevlen = -1; // last emitted length
            int nextlen = tree[(0 * 2) + 1]; // length of next code
            var count = 0; // repeat count of the current code
            var maxCount = 7; // max repeat count
            var minCount = 4; // min repeat count
            if (nextlen is 0)
            {
                maxCount = 138;
                minCount = 3;
            }

            for (n = 0; n <= maxCode; n++)
            {
                var curlen = nextlen; // length of current code
                nextlen = tree[((n + 1) * 2) + 1];
                if (++count < maxCount && curlen == nextlen)
                {
                    continue;
                }
                else if (count < minCount)
                {
                    do
                    {
                        this.Send_code(curlen, this.BlTree);
                    }
                    while (--count != 0);
                }
                else if (curlen != 0)
                {
                    if (curlen != prevlen)
                    {
                        this.Send_code(curlen, this.BlTree);
                        count--;
                    }

                    this.Send_code(16, this.BlTree);
                    this.Send_bits(count - 3, 2);
                }
                else if (count <= 10)
                {
                    this.Send_code(17, this.BlTree);
                    this.Send_bits(count - 3, 3);
                }
                else
                {
                    this.Send_code(18, this.BlTree);
                    this.Send_bits(count - 11, 7);
                }

                count = 0;
                prevlen = curlen;
                maxCount = nextlen is 0 ? 138 : curlen == nextlen ? 6 : 7;
                minCount = nextlen is 0 || curlen == nextlen ? 3 : 4;
            }
        }

        // Output a byte on the stream.
        // IN assertion: there is enough room in pending_buf.
        private void Put_byte(byte[] p, int start, int len)
        {
            Array.Copy(p, start, this.PendingBuf, this.Pending, len);
            this.Pending += len;
        }

        private void Put_byte(byte c)
            => this.PendingBuf[this.Pending++] = c;

        private void Put_short(int w)
        {
            this.Put_byte((byte)w);
            this.Put_byte((byte)(w >= 0 ? w >> 8 : (w >> 8) + 16777216));
        }

        private void PutShortMsb(int b)
        {
            this.Put_byte((byte)(b >> 8));
            this.Put_byte((byte)b);
        }

        private void Send_code(int c, short[] tree)
            => this.Send_bits(tree[c * 2] & 0xffff, tree[(c * 2) + 1] & 0xffff);

        private void Send_bits(int valueRenamed, int length)
        {
            var len = length;
            if (this.BiValid > 16 - len)
            {
                var val = valueRenamed;
                this.BiBuf = (short)((ushort)this.BiBuf | (ushort)((val << this.BiValid) & 0xffff));
                this.Put_short(this.BiBuf);
                this.BiBuf = (short)(val >= 0 ? val >> (16 - this.BiValid) : (val >> (16 - this.BiValid)) + (2 << ~(16 - this.BiValid)));
                this.BiValid += len - 16;
            }
            else
            {
                this.BiBuf = (short)((ushort)this.BiBuf | (ushort)((valueRenamed << this.BiValid) & 0xffff));
                this.BiValid += len;
            }
        }

        // Send one empty static block to give enough lookahead for inflate.
        // This takes 10 bits, of which 7 may remain in the bit buffer.
        // The current inflate code requires 9 bits of lookahead. If the
        // last two codes for the previous block (real code plus EOB) were coded
        // on 5 bits or less, inflate may have only 5+3 bits of lookahead to decode
        // the last real code. In this case we send two empty static blocks instead
        // of one. (There are no problems if the previous block is stored or fixed.)
        // To simplify the code, we assume the worst case of last real code encoded
        // on one bit only.
        private void Tr_align()
        {
            this.Send_bits(2, 3);
            this.Send_code(256, StaticTree.StaticLtree.ToArray());
            this.Bi_flush();

            // Of the 10 bits for the empty block, we have already sent
            // (10 - bi_valid) bits. The lookahead for the last real code (before
            // the EOB of the previous block) was thus at least one plus the length
            // of the EOB plus what we have just sent of the empty static block.
            if (1 + this.LastEobLen + 10 - this.BiValid < 9)
            {
                this.Send_bits(2, 3);
                this.Send_code(256, StaticTree.StaticLtree.ToArray());
                this.Bi_flush();
            }

            this.LastEobLen = 7;
        }

        // Save the match info and tally the frequency counts. Return true if
        // the current block must be flushed.
        private bool Tr_tally(int dist, int lc)
        {
            this.PendingBuf[this.DBuf + (this.LastLit * 2)] = (byte)(dist >= 0 ? dist >> 8 : (dist >> 8) + 16777216);
            this.PendingBuf[this.DBuf + (this.LastLit * 2) + 1] = (byte)dist;
            this.PendingBuf[this.LBuf + this.LastLit] = (byte)lc;
            this.LastLit++;
            if (dist is 0)
            {
                // lc is the unmatched char
                this.DynLtree[lc * 2]++;
            }
            else
            {
                this.Matches++;

                // Here, lc is the match length - MIN_MATCH
                dist--; // dist = match distance - 1
                this.DynLtree[(Tree.LengthCode[lc] + 257) * 2]++;
                this.DynDtree[Tree.D_code(dist) * 2]++;
            }

            if ((this.LastLit & 0x1fff) == 0 && this.Level > ZlibCompression.Level2)
            {
                // Compute an upper bound for the compressed length
                var outLength = this.LastLit * 8;
                var inLength = this.Strstart - this.BlockStart;
                int dcode;
                for (dcode = 0; dcode < 30; dcode++)
                {
                    outLength = (int)(outLength + (this.DynDtree[dcode * 2] * (5L + Tree.ExtraDbits[dcode])));
                }

                outLength = outLength >= 0 ? outLength >> 3 : (outLength >> 3) + 536870912;
                if (this.Matches < this.LastLit / 2 && outLength < inLength / 2)
                {
                    return true;
                }
            }

            return this.LastLit == this.LitBufsize - 1;

            // We avoid equality with lit_bufsize because of wraparound at 64K
            // on 16 bit machines and because stored blocks are restricted to
            // 64K-1 bytes.
        }

        // Send the block data compressed using the given Huffman trees
        private void Compress_block(ReadOnlySpan<short> ltree, ReadOnlySpan<short> dtree)
        {
            var lx = 0; // running index in l_buf
            if (this.LastLit is not 0)
            {
                do
                {
                    var dist = ((this.PendingBuf[this.DBuf + (lx * 2)] << 8) & 0xff00) | (this.PendingBuf[this.DBuf + (lx * 2) + 1] & 0xff); // distance of matched string
                    var lc = this.PendingBuf[this.LBuf + lx] & 0xff; // match length or unmatched char (if dist == 0)
                    lx++;
                    if (dist is 0)
                    {
                        this.Send_code(lc, ltree.ToArray()); // send a literal byte
                    }
                    else
                    {
                        // Here, lc is the match length - MIN_MATCH
                        int code = Tree.LengthCode[lc]; // the code to send
                        this.Send_code(code + 257, ltree.ToArray()); // send the length code
                        var extra = Tree.ExtraLbits[code]; // number of extra bits to send
                        if (extra is not 0)
                        {
                            lc -= Tree.BaseLength[code];
                            this.Send_bits(lc, extra); // send the extra length bits
                        }

                        dist--; // dist is now the match distance - 1
                        code = Tree.D_code(dist);
                        this.Send_code(code, dtree.ToArray()); // send the distance code
                        extra = Tree.ExtraDbits[code];
                        if (extra is not 0)
                        {
                            dist -= Tree.BaseDist[code];
                            this.Send_bits(dist, extra); // send the extra distance bits
                        }
                    } // literal or match pair ?

                    // Check that the overlay between pending_buf and d_buf+l_buf is ok:
                }
                while (lx < this.LastLit);
            }

            this.Send_code(256, ltree.ToArray());
            this.LastEobLen = ltree[513];
        }

        // Set the data type to ASCII or BINARY, using a crude approximation:
        // binary if more than 20% of the bytes are <= 6 or >= 128, ascii otherwise.
        // IN assertion: the fields freq of dyn_ltree are set and the total of all
        // frequencies does not exceed 64K (to fit in an int on 16 bit machines).
        private void Set_data_type()
        {
            var n = 0;
            var asciiFreq = 0;
            var binFreq = 0;
            while (n < 7)
            {
                binFreq += this.DynLtree[n * 2];
                n++;
            }

            while (n < 128)
            {
                asciiFreq += this.DynLtree[n * 2];
                n++;
            }

            while (n < 256)
            {
                binFreq += this.DynLtree[n * 2];
                n++;
            }

            this.DataType = (byte)(binFreq > (asciiFreq >= 0 ? asciiFreq >> 2 : (asciiFreq >> 2) + 1073741824) ? 0 : 1);
        }

        // Flush the bit buffer, keeping at most 7 bits in it.
        private void Bi_flush()
        {
            switch (this.BiValid)
            {
                case 16:
                    this.Put_short(this.BiBuf);
                    this.BiBuf = 0;
                    this.BiValid = 0;
                    break;
                case >= 8:
                    this.Put_byte((byte)this.BiBuf);
                    this.BiBuf = (short)(this.BiBuf >= 0 ? this.BiBuf >> 8 : (this.BiBuf >> 8) + 16777216);
                    this.BiValid -= 8;
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        // Flush the bit buffer and align the output on a byte boundary
        private void Bi_windup()
        {
            switch (this.BiValid)
            {
                case > 8:
                    this.Put_short(this.BiBuf);
                    break;
                case > 0:
                    this.Put_byte((byte)this.BiBuf);
                    break;
                default:
                    throw new InvalidOperationException();
            }

            this.BiBuf = 0;
            this.BiValid = 0;
        }

        // Copy a stored block, storing first the length and its
        // one's complement if requested.
        private void Copy_block(int buf, int len, bool header)
        {
            this.Bi_windup(); // align on byte boundary
            this.LastEobLen = 8; // enough lookahead for inflate
            if (header)
            {
                this.Put_short((short)len);
                this.Put_short((short)~len);
            }

            this.Put_byte(this.Window, buf, len);
        }

        private void Flush_block_only(bool eof)
        {
            this.Tr_flush_block(this.BlockStart >= 0 ? this.BlockStart : -1, this.Strstart - this.BlockStart, eof);
            this.BlockStart = this.Strstart;
            this.Strm.Flush_pending();
        }

        // Copy without compression as much as possible from the input stream, return
        // the current block state.
        // This function does not insert new strings in the dictionary since
        // uncompressible data is probably not useful. This function is used
        // only for the level=0 compression option.
        // NOTE: this function should be optimized to avoid extra copying from
        // window to pending_buf.
        private int Deflate_stored(ZlibFlushStrategy flush)
        {
            // Stored blocks are limited to 0xffff bytes, pending_buf is limited
            // to pending_buf_size, and each stored block has a 5 byte header:
            var maxBlockSize = 0xffff;
            if (maxBlockSize > this.PendingBufSize - 5)
            {
                maxBlockSize = this.PendingBufSize - 5;
            }

            // Copy as much as possible from input to output:
            while (true)
            {
                // Fill the window as much as possible:
                if (this.Lookahead <= 1)
                {
                    this.Fill_window();
                    if (this.Lookahead is 0 && flush is ZlibFlushStrategy.NoFlush)
                    {
                        return 0;
                    }

                    if (this.Lookahead is 0)
                    {
                        break; // flush the current block
                    }
                }

                this.Strstart += this.Lookahead;
                this.Lookahead = 0;

                // Emit a stored block if pending_buf will be full:
                var maxStart = this.BlockStart + maxBlockSize;
                if (this.Strstart is 0 || this.Strstart >= maxStart)
                {
                    // strstart == 0 is possible when wraparound on 16-bit machine
                    this.Lookahead = this.Strstart - maxStart;
                    this.Strstart = maxStart;
                    this.Flush_block_only(false);
                    if (this.Strm.AvailOut is 0)
                    {
                        return 0;
                    }
                }

                // Flush if we may have to slide, otherwise block_start may become
                // negative and the data will be gone:
                if (this.Strstart - this.BlockStart >= this.WSize - 262)
                {
                    this.Flush_block_only(false);
                    if (this.Strm.AvailOut is 0)
                    {
                        return 0;
                    }
                }
            }

            this.Flush_block_only(flush is ZlibFlushStrategy.Finish);
            return this.Strm.AvailOut is 0 ? flush is ZlibFlushStrategy.Finish ? 2 : 0 : flush is ZlibFlushStrategy.Finish ? 3 : 1;
        }

        // Send a stored block
        private void Tr_stored_block(int buf, int storedLen, bool eof)
        {
            this.Send_bits(0 + (eof ? 1 : 0), 3); // send block type
            this.Copy_block(buf, storedLen, true); // with header
        }

        // Determine the best encoding for the current block: dynamic trees, static
        // trees or store, and output the encoded block to the zip file.
        private void Tr_flush_block(int buf, int storedLen, bool eof)
        {
            int optLenb, staticLenb; // opt_len and static_len in bytes
            var maxBlindex = 0; // index of last bit length code of non zero freq

            // Build the Huffman trees unless a stored block is forced
            if (this.Level > 0)
            {
                // Check if the file is ascii or binary
                if (this.DataType == 2)
                {
                    this.Set_data_type();
                }

                // Construct the literal and distance trees
                this.LDesc.Build_tree(this);
                this.DDesc.Build_tree(this);

                // At this point, opt_len and static_len are the total bit lengths of
                // the compressed block data, excluding the tree representations.

                // Build the bit length tree for the above two trees, and get the index
                // in bl_order of the last bit length code to send.
                maxBlindex = this.Build_bl_tree();

                // Determine the best encoding. Compute first the block length in bytes
                optLenb = this.OptLen + 3 + 7 >= 0 ? (this.OptLen + 3 + 7) >> 3 : ((this.OptLen + 3 + 7) >> 3) + 536870912;
                staticLenb = this.StaticLen + 3 + 7 >= 0 ? (this.StaticLen + 3 + 7) >> 3 : ((this.StaticLen + 3 + 7) >> 3) + 536870912;
                if (staticLenb <= optLenb)
                {
                    optLenb = staticLenb;
                }
            }
            else
            {
                optLenb = staticLenb = storedLen + 5; // force a stored block
            }

            if (storedLen + 4 <= optLenb && buf != -1)
            {
                // 4: two words for the lengths
                // The test buf != NULL is only necessary if LIT_BUFSIZE > WSIZE.
                // Otherwise we can't have processed more than WSIZE input bytes since
                // the last block flush, because compression would have been
                // successful. If LIT_BUFSIZE <= WSIZE, it is never too late to
                // transform a block into a stored block.
                this.Tr_stored_block(buf, storedLen, eof);
            }
            else if (staticLenb == optLenb)
            {
                this.Send_bits(2 + (eof ? 1 : 0), 3);
                this.Compress_block(StaticTree.StaticLtree, StaticTree.StaticDtree);
            }
            else
            {
                this.Send_bits(4 + (eof ? 1 : 0), 3);
                this.Send_all_trees(this.LDesc.MaxCode + 1, this.DDesc.MaxCode + 1, maxBlindex + 1);
                this.Compress_block(this.DynLtree, this.DynDtree);
            }

            // The above check is made mod 2^32, for files larger than 512 MB
            // and uLong implemented on 32 bits.
            this.Init_block();
            if (eof)
            {
                this.Bi_windup();
            }
        }

        // Fill the window when the lookahead becomes insufficient.
        // Updates strstart and lookahead.
        //
        // IN assertion: lookahead < MIN_LOOKAHEAD
        // OUT assertions: strstart <= window_size-MIN_LOOKAHEAD
        //    At least one byte has been read, or avail_in == 0; reads are
        //    performed for at least two bytes (required for the zip translate_eol
        //    option -- not supported here).
        private void Fill_window()
        {
            do
            {
                var more = this.WindowSize - this.Lookahead - this.Strstart; // Amount of free space at the end of the window.

                // Deal with !@#$% 64K limit:
                int n;
                switch (more)
                {
                    case 0 when this.Strstart == 0 && this.Lookahead == 0:
                        more = this.WSize;
                        break;

                    case -1:
                        // Very unlikely, but possible on 16 bit machine if strstart == 0
                        // and lookahead == 1 (input done one byte at time)
                        more--;

                        // If the window is almost full and there is insufficient lookahead,
                        // move the upper half to the lower one to make room in the upper half.
                        break;

                    default:
                    {
                        if (this.Strstart >= this.WSize + this.WSize - 262)
                        {
                            Array.Copy(this.Window, this.WSize, this.Window, 0, this.WSize);
                            this.MatchStart -= this.WSize;
                            this.Strstart -= this.WSize; // we now have strstart >= MAX_DIST
                            this.BlockStart -= this.WSize;

                            // Slide the hash table (could be avoided with 32 bit values
                            // at the expense of memory usage). We slide even when level == 0
                            // to keep the hash table consistent if we switch back to level > 0
                            // later. (Using level 0 permanently is not an optimal usage of
                            // zlib, so we don't care about this pathological case.)
                            n = this.HashSize;
                            var p = n;
                            int m;
                            do
                            {
                                m = this.Head[--p] & 0xffff;
                                this.Head[p] = (short)(m >= this.WSize ? m - this.WSize : 0);
                            }
                            while (--n != 0);
                            n = this.WSize;
                            p = n;
                            do
                            {
                                m = this.Prev[--p] & 0xffff;
                                this.Prev[p] = (short)(m >= this.WSize ? m - this.WSize : 0);

                                // If n is not on any hash chain, prev[n] is garbage but
                                // its value will never be used.
                            }
                            while (--n != 0);
                            more += this.WSize;
                        }

                        break;
                    }
                }

                if (this.Strm.AvailIn == 0)
                {
                    return;
                }

                // If there was no sliding:
                //    strstart <= WSIZE+MAX_DIST-1 && lookahead <= MIN_LOOKAHEAD - 1 &&
                //    more == window_size - lookahead - strstart
                // => more >= window_size - (MIN_LOOKAHEAD-1 + WSIZE + MAX_DIST-1)
                // => more >= window_size - 2*WSIZE + 2
                // In the BIG_MEM or MMAP case (not yet supported),
                //   window_size == input_size + MIN_LOOKAHEAD  &&
                //   strstart + s->lookahead <= input_size => more >= MIN_LOOKAHEAD.
                // Otherwise, window_size == 2*WSIZE so more >= 2.
                // If there was sliding, more >= WSIZE. So in all cases, more >= 2.
                n = this.Strm.Read_buf(this.Window, this.Strstart + this.Lookahead, more);
                this.Lookahead += n;

                // Initialize the hash value now that we have some input:
                if (this.Lookahead >= 3)
                {
                    this.InsH = this.Window[this.Strstart] & 0xff;
                    this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 1] & 0xff)) & this.HashMask;
                }

                // If the whole input has less than MIN_MATCH bytes, ins_h is garbage,
                // but this is not important since only literal bytes will be emitted.
            }
            while (this.Lookahead < 262 && this.Strm.AvailIn != 0);
        }

        // Compress as much as possible from the input stream, return the current
        // block state.
        // This function does not perform lazy evaluation of matches and inserts
        // new strings in the dictionary only for unmatched strings or for short
        // matches. It is used only for the fast compression options.
        private int Deflate_fast(ZlibFlushStrategy flush)
        {
            // short hash_head = 0; // head of the hash chain
            var hashHead = 0; // head of the hash chain
            while (true)
            {
                // Make sure that we always have enough lookahead, except
                // at the end of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.
                if (this.Lookahead < 262)
                {
                    this.Fill_window();
                    if (this.Lookahead < 262 && flush is ZlibFlushStrategy.NoFlush)
                    {
                        return 0;
                    }

                    if (this.Lookahead is 0)
                    {
                        break; // flush the current block
                    }
                }

                // Insert the string window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:
                if (this.Lookahead >= 3)
                {
                    this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 2] & 0xff)) & this.HashMask;
                    hashHead = this.Head[this.InsH] & 0xffff;
                    this.Prev[this.Strstart & this.WMask] = this.Head[this.InsH];
                    this.Head[this.InsH] = (short)this.Strstart;
                }

                // Find the longest match, discarding those <= prev_length.
                // At this point we have always match_length < MIN_MATCH
                // To simplify the code, we prevent matches with the string
                // of window index 0 (in particular we have to avoid a match
                // of the string with itself at the start of the input file).
                if (hashHead != 0L && ((this.Strstart - hashHead) & 0xffff) <= this.WSize - 262 && this.Strategy is not ZlibCompressionStrategy.HuffmanOnly)
                {
                    this.MatchLength = this.Longest_match(hashHead);

                    // longest_match() sets match_start
                }

                bool bflush; // set if current block must be flushed
                if (this.MatchLength >= 3)
                {
                    bflush = this.Tr_tally(this.Strstart - this.MatchStart, this.MatchLength - 3);
                    this.Lookahead -= this.MatchLength;

                    // Insert new strings in the hash table only if the match length
                    // is not too large. This saves time but degrades compression.
                    if (this.MatchLength <= this.MaxLazyMatch && this.Lookahead >= 3)
                    {
                        this.MatchLength--; // string at strstart already in hash table
                        do
                        {
                            this.Strstart++;
                            this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 2] & 0xff)) & this.HashMask;
                            hashHead = this.Head[this.InsH] & 0xffff;
                            this.Prev[this.Strstart & this.WMask] = this.Head[this.InsH];
                            this.Head[this.InsH] = (short)this.Strstart;

                            // strstart never exceeds WSIZE-MAX_MATCH, so there are
                            // always MIN_MATCH bytes ahead.
                        }
                        while (--this.MatchLength != 0);
                        this.Strstart++;
                    }
                    else
                    {
                        this.Strstart += this.MatchLength;
                        this.MatchLength = 0;
                        this.InsH = this.Window[this.Strstart] & 0xff;
                        this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 1] & 0xff)) & this.HashMask;

                        // If lookahead < MIN_MATCH, ins_h is garbage, but it does not
                        // matter since it will be recomputed at next deflate call.
                    }
                }
                else
                {
                    // No match, output a literal byte
                    bflush = this.Tr_tally(0, this.Window[this.Strstart] & 0xff);
                    this.Lookahead--;
                    this.Strstart++;
                }

                if (bflush)
                {
                    this.Flush_block_only(false);
                    if (this.Strm.AvailOut is 0)
                    {
                        return 0;
                    }
                }
            }

            this.Flush_block_only(flush is ZlibFlushStrategy.Finish);
            return this.Strm.AvailOut is 0 ? flush is ZlibFlushStrategy.Finish ? 2 : 0 : flush is ZlibFlushStrategy.Finish ? 3 : 1;
        }

        // Same as above, but achieves better compression. We use a lazy
        // evaluation for matches: a match is finally adopted only if there is
        // no better match at the next window position.
        private int Deflate_slow(ZlibFlushStrategy flush)
        {
            // short hash_head = 0;    // head of hash chain
            var hashHead = 0; // head of hash chain

            // Process the input block.
            while (true)
            {
                // Make sure that we always have enough lookahead, except
                // at the end of the input file. We need MAX_MATCH bytes
                // for the next match, plus MIN_MATCH bytes to insert the
                // string following the next match.
                if (this.Lookahead < 262)
                {
                    this.Fill_window();
                    if (this.Lookahead < 262 && flush is ZlibFlushStrategy.NoFlush)
                    {
                        return 0;
                    }

                    if (this.Lookahead is 0)
                    {
                        break; // flush the current block
                    }
                }

                // Insert the string window[strstart .. strstart+2] in the
                // dictionary, and set hash_head to the head of the hash chain:
                if (this.Lookahead >= 3)
                {
                    this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 2] & 0xff)) & this.HashMask;
                    hashHead = this.Head[this.InsH] & 0xffff;
                    this.Prev[this.Strstart & this.WMask] = this.Head[this.InsH];
                    this.Head[this.InsH] = (short)this.Strstart;
                }

                // Find the longest match, discarding those <= prev_length.
                this.PrevLength = this.MatchLength;
                this.PrevMatch = this.MatchStart;
                this.MatchLength = 2;
                if (hashHead is not 0 && this.PrevLength < this.MaxLazyMatch && ((this.Strstart - hashHead) & 0xffff) <= this.WSize - 262)
                {
                    // To simplify the code, we prevent matches with the string
                    // of window index 0 (in particular we have to avoid a match
                    // of the string with itself at the start of the input file).
                    if (this.Strategy is not ZlibCompressionStrategy.HuffmanOnly)
                    {
                        this.MatchLength = this.Longest_match(hashHead);
                    }

                    // longest_match() sets match_start
                    if (this.MatchLength <= 5 && (this.Strategy is ZlibCompressionStrategy.Filtered || (this.MatchLength is 3 && this.Strstart - this.MatchStart > 4096)))
                    {
                        // If prev_match is also MIN_MATCH, match_start is garbage
                        // but we will ignore the current match anyway.
                        this.MatchLength = 2;
                    }
                }

                // If there was a match at the previous step and the current
                // match is not better, output the previous match:
                bool bflush; // set if current block must be flushed
                if (this.PrevLength >= 3 && this.MatchLength <= this.PrevLength)
                {
                    var maxInsert = this.Strstart + this.Lookahead - 3;

                    // Do not insert strings in hash table beyond this.
                    bflush = this.Tr_tally(this.Strstart - 1 - this.PrevMatch, this.PrevLength - 3);

                    // Insert in hash table all strings up to the end of the match.
                    // strstart-1 and strstart are already inserted. If there is not
                    // enough lookahead, the last two strings are not inserted in
                    // the hash table.
                    this.Lookahead -= this.PrevLength - 1;
                    this.PrevLength -= 2;
                    do
                    {
                        if (++this.Strstart <= maxInsert)
                        {
                            this.InsH = ((this.InsH << this.HashShift) ^ (this.Window[this.Strstart + 2] & 0xff)) & this.HashMask;
                            hashHead = this.Head[this.InsH] & 0xffff;
                            this.Prev[this.Strstart & this.WMask] = this.Head[this.InsH];
                            this.Head[this.InsH] = (short)this.Strstart;
                        }
                    }
                    while (--this.PrevLength != 0);
                    this.MatchAvailable = 0;
                    this.MatchLength = 2;
                    this.Strstart++;
                    if (bflush)
                    {
                        this.Flush_block_only(false);
                        if (this.Strm.AvailOut == 0)
                        {
                            return 0;
                        }
                    }
                }
                else if (this.MatchAvailable is not 0)
                {
                    // If there was no match at the previous position, output a
                    // single literal. If there was a match but the current match
                    // is longer, truncate the previous match to a single literal.
                    bflush = this.Tr_tally(0, this.Window[this.Strstart - 1] & 0xff);
                    if (bflush)
                    {
                        this.Flush_block_only(false);
                    }

                    this.Strstart++;
                    this.Lookahead--;
                    if (this.Strm.AvailOut is 0)
                    {
                        return 0;
                    }
                }
                else
                {
                    // There is no previous match to compare with, wait for
                    // the next step to decide.
                    this.MatchAvailable = 1;
                    this.Strstart++;
                    this.Lookahead--;
                }
            }

            if (this.MatchAvailable is not 0)
            {
                _ = this.Tr_tally(0, this.Window[this.Strstart - 1] & 0xff);
                this.MatchAvailable = 0;
            }

            this.Flush_block_only(flush is ZlibFlushStrategy.Finish);
            return this.Strm.AvailOut is 0 ? flush is ZlibFlushStrategy.Finish ? 2 : 0 : flush is ZlibFlushStrategy.Finish ? 3 : 1;
        }

        private int Longest_match(int curMatch)
        {
            var chainLength = this.MaxChainLength; // max hash chain length
            var scan = this.Strstart; // current string
            var bestLen = this.PrevLength; // best match length so far
            var limit = this.Strstart > this.WSize - 262 ? this.Strstart - (this.WSize - 262) : 0;
            var niceMatch = this.NiceMatch;

            // Stop when cur_match becomes <= limit. To simplify the code,
            // we prevent matches with the string of window index 0.
            var wmask = this.WMask;
            var strend = this.Strstart + 258;
            var scanEnd1 = this.Window[scan + bestLen - 1];
            var scanEnd = this.Window[scan + bestLen];

            // The code is optimized for HASH_BITS >= 8 and MAX_MATCH-2 multiple of 16.
            // It is easy to get rid of this optimization if necessary.
            // Do not waste too much time if we already have a good match:
            if (this.PrevLength >= this.GoodMatch)
            {
                chainLength >>= 2;
            }

            // Do not look for matches beyond the end of the input. This is necessary
            // to make deflate deterministic.
            if (niceMatch > this.Lookahead)
            {
                niceMatch = this.Lookahead;
            }

            do
            {
                var match = curMatch; // matched string

                // Skip to next match if the match length cannot increase
                // or if the match length is less than 2:
                if (this.Window[match + bestLen] != scanEnd || this.Window[match + bestLen - 1] != scanEnd1 || this.Window[match] != this.Window[scan] || this.Window[++match] != this.Window[scan + 1])
                {
                    continue;
                }

                // The check at best_len-1 can be removed because it will be made
                // again later. (This heuristic is not always a win.)
                // It is not necessary to compare scan[2] and match[2] since they
                // are always equal when the other bytes match, given that
                // the hash keys are equal and that HASH_BITS >= 8.
                scan += 2;
                match++;

                // We check for insufficient lookahead only every 8th
                // comparison; the 256th check will be made at strstart+258.
                do
                {
                    // nothing here.
                }
                while (this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && this.Window[++scan] == this.Window[++match] && scan < strend);
                var len = 258 - (strend - scan); // length of current match
                scan = strend - 258;
                if (len > bestLen)
                {
                    this.MatchStart = curMatch;
                    bestLen = len;
                    if (len >= niceMatch)
                    {
                        break;
                    }

                    scanEnd1 = this.Window[scan + bestLen - 1];
                    scanEnd = this.Window[scan + bestLen];
                }
            }
            while ((curMatch = this.Prev[curMatch & wmask] & 0xffff) > limit && --chainLength != 0);
            return bestLen <= this.Lookahead ? bestLen : this.Lookahead;
        }

        private ZlibCompressionState DeflateInit2(ZlibStream stream, ZlibCompression level, int method, int windowBits, int memLevel, ZlibCompressionStrategy strategy)
        {
            var noheader = 0;
            stream.Msg = null;
            if (level is ZlibCompression.DefaultCompression)
            {
                level = ZlibCompression.Level6;
            }

            if (windowBits < 0)
            {
                // undocumented feature: suppress zlib header
                noheader = 1;
                windowBits = -windowBits;
            }

            if (memLevel < 1 || memLevel > 9 || method is not 8 || windowBits < 9 || windowBits > 15 || level < ZlibCompression.NoCompression || level > ZlibCompression.BestCompression || strategy < ZlibCompressionStrategy.DefaultStrategy || strategy > ZlibCompressionStrategy.HuffmanOnly)
            {
                return ZlibCompressionState.StreamError;
            }

            stream.DState = this;
            this.Noheader = noheader;
            this.WBits = windowBits;
            this.WSize = 1 << this.WBits;
            this.WMask = this.WSize - 1;
            this.HashBits = memLevel + 7;
            this.HashSize = 1 << this.HashBits;
            this.HashMask = this.HashSize - 1;
            this.HashShift = (this.HashBits + 2) / 3;
            this.Window = new byte[this.WSize * 2];
            this.Prev = new short[this.WSize];
            this.Head = new short[this.HashSize];
            this.LitBufsize = 1 << (memLevel + 6); // 16K elements by default

            // We overlay pending_buf and d_buf+l_buf. This works since the
            // average output size for (length, distance) codes
            // is <= 24 bits.
            this.PendingBuf = new byte[this.LitBufsize * 4];
            this.PendingBufSize = this.LitBufsize * 4;
            this.DBuf = this.LitBufsize;
            this.LBuf = 3 * this.LitBufsize;
            this.Level = level;
            this.Strategy = strategy;
            this.Method = (byte)method;
            return this.DeflateReset(stream);
        }

        private ZlibCompressionState DeflateReset(ZlibStream stream)
        {
            stream.TotalIn = stream.TotalOut = 0;
            stream.Msg = null;
            stream.DataType = 2;
            this.Pending = 0;
            this.PendingOut = 0;
            if (this.Noheader < 0)
            {
                // was set to -1 by deflate(..., Z_FINISH).
                this.Noheader = 0;
            }

            this.Status = this.Noheader is not 0 ? 113 : 42;
            stream.Adler = Adler32.Calculate(0, null, 0, 0);
            this.LastFlush = ZlibFlushStrategy.NoFlush;
            this.Tr_init();
            this.Lm_init();
            return ZlibCompressionState.Ok;
        }

        private class Config
        {
            internal Config(int goodLength, int maxLazy, int niceLength, int maxChain, int func)
            {
                this.GoodLength = goodLength;
                this.MaxLazy = maxLazy;
                this.NiceLength = niceLength;
                this.MaxChain = maxChain;
                this.Func = func;
            }

            // reduce lazy search above this match length
            internal int GoodLength { get; }

            // do not perform lazy search above this match length
            internal int MaxLazy { get; }

            // quit search above this match length
            internal int NiceLength { get; }

            internal int MaxChain { get; }

            internal int Func { get; }
        }
    }
}
