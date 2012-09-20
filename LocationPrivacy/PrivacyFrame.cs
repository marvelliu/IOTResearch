using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdHocBaseApp;

namespace LocationPrivacy
{
    public class FrameList
    {
        public bool IsStrictPolicy;
        HashSet<Frame> frames = new HashSet<Frame>();
        List<FrameNode> nodes = new List<FrameNode>();
        HashSet<Frame> allframes = new HashSet<Frame>();

        public void Init(HashSet<int> list, int initNodeId, bool IsStrictPolicy)
        {
            Frame newFrame = new Frame(0, list.Count-1);
            this.frames.Add(newFrame);
            this.allframes.Add(newFrame);

            this.nodes.Add(new FrameNode(initNodeId, true, newFrame.frameId));
            foreach (int nodeId in list)
            {
                if(nodeId != initNodeId)
                    this.nodes.Add(new FrameNode(nodeId, false, newFrame.frameId));
            }

            this.IsStrictPolicy = IsStrictPolicy;
        }

        public int GetFrameCount()
        {
            return this.frames.Count;
        }

        /*
        public int GetNodeIndex(FrameNode node)
        {
            Frame frame =  node.belongedFrameId;
            for (int i = frame.start; i <= frame.end; i++)
            {
                if (nodes[i].nodeId == node.nodeId)
                    return i;
            }
            return -1;
        }*/

        private HashSet<int> jointNodeIds, childNodeIds;

        bool IsNodeAvailable(int nodeId)
        {
            return !jointNodeIds.Contains(nodeId) && childNodeIds.Contains(nodeId);
        }

        bool IsChild(int nodeId)
        {
            return childNodeIds.Contains(nodeId);
        }

        public void DumpNodes()
        {
            DumpNodes(this.nodes);
        }
        public void DumpNodes(List<FrameNode> lnodes)
        {

            for (int i = 0; i < lnodes.Count; i++)
            {
                FrameNode node = lnodes[i];
                Console.Write("{0},", node.nodeId);
            }
            Console.WriteLine();
        }

        public void DumpFrames(int nodeId)
        {
            DumpFrames(this.frames, this.nodes, nodeId);
        }

        public void DumpFrames(HashSet<Frame> lframes, List<FrameNode> lnodes, Dictionary<int, Frame> frameIdIndexMapping, int nodeId)
        {
            Scheduler scheduler = Scheduler.getInstance();

            if (lframes.Count == 0)
                return;
            int lastframe = -1;
            for (int i = 0; i < lnodes.Count; i++)
            {
                FrameNode node = lnodes[i];
                if (node.belongedFrameId != lastframe)
                {
                    Frame frame = frameIdIndexMapping[node.belongedFrameId];
                    if (lastframe == -1)
                        Console.Write("{0:F4} [CUR_GROUP] READER{1} [{2},{3}-{4}](", scheduler.currentTime, nodeId, frame.frameId, frame.start, frame.end);
                    else
                        Console.Write(")\t[{0},{1}-{2}](", frame.frameId, frame.start, frame.end);
                    lastframe = node.belongedFrameId;
                }
                else
                    Console.Write(",");
                if (node.anchor)
                    Console.Write("{0}+", node.nodeId);
                else
                    Console.Write("{0}", node.nodeId);
            }
            Console.WriteLine(")");
        }



        public void DumpFrames(HashSet<Frame> lframes, List<FrameNode> lnodes, int nodeId)
        {
            Scheduler scheduler = Scheduler.getInstance();
            if (lframes.Count == 0)
                return;

            Dictionary<int, Frame> frameIdIndexMapping = new Dictionary<int, Frame>();

            foreach(Frame frame in lframes)
            {
                frameIdIndexMapping.Add(frame.frameId, frame);
            }

            int lastframe = -1;
            for (int i = 0; i < lnodes.Count; i++)
            {
                FrameNode node = lnodes[i];
                if (node.belongedFrameId != lastframe)
                {
                    Frame frame = frameIdIndexMapping[node.belongedFrameId];
                    if (lastframe == -1)
                        Console.Write("{0:F4} [CUR_GROUP] READER{1} [{2},{3}-{4}](", scheduler.currentTime, nodeId, frame.frameId, frame.start, frame.end);
                    else
                        Console.Write(")\t[{0},{1}-{2}](", frame.frameId, frame.start, frame.end);
                    lastframe = node.belongedFrameId;
                }
                else
                    Console.Write(",");
                if (node.anchor)
                    Console.Write("{0}+", node.nodeId);
                else
                    Console.Write("{0}", node.nodeId);
            }
            Console.WriteLine(")");
        }


        public HashSet<int> AddNewGroup(int origId, int currentId, int k, AnonyGroupEntry anonGroups,
            Dictionary<int, SubTreeEntry> subtree, ref List<int> swapList)
        {
            PrivacyGlobal global = (PrivacyGlobal)Global.getInstance();

            HashSet<int> newgroup = new HashSet<int>();
            swapList = new List<int>();
            int step;


            //预处理 复制一个相同的数组
            HashSet<Frame> lframes = new HashSet<Frame>();
            HashSet<Frame> lallframes = new HashSet<Frame>();
            List<FrameNode> lnodes = new List<FrameNode>();

            //建立映射关系
            Dictionary<FrameNode, int> nodeIndexMapping = new Dictionary<FrameNode, int>();
            Dictionary<int, int> nodeIdIndexMapping = new Dictionary<int, int>();
            Dictionary<int, Frame> frameIdIndexMapping = new Dictionary<int, Frame>();


            foreach (Frame frame in allframes)
            { 
                Frame framecopy = (Frame)frame.Clone();
                if (frames.Contains(frame))
                {
                    lframes.Add(framecopy);
                    frameIdIndexMapping.Add(frame.frameId, framecopy);
                }
                lallframes.Add(framecopy);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                lnodes.Add((FrameNode)nodes[i].Clone());
                FrameNode node = lnodes[i];
                nodeIndexMapping.Add(node, i);
                nodeIdIndexMapping.Add(node.nodeId, i);
            }


            //步骤1 将框内所有已加入k-匿名组的节点置为不可用，这个对框结构没有影响
            step = 1;
            jointNodeIds = new HashSet<int>();
            foreach (int c in subtree.Keys)
            {
                if (anonGroups.subUnavailAnonyNodes.ContainsKey(c + "-" + k))
                {
                    foreach (int nodeId in anonGroups.subUnavailAnonyNodes[c + "-" + k])
                        jointNodeIds.Add(nodeId);
                }
            }


        STEP2:
            //步骤2 将非本节点子树的节点去掉，将原始框分割为新的框集，这个会减少框的大小
            if (step < 4)
                step = 2;
            childNodeIds = new HashSet<int>();
            foreach (FrameNode node in lnodes)
            {
                int nodeId = node.nodeId;
                if (subtree.ContainsKey(nodeId) || currentId == nodeId)
                {
                    childNodeIds.Add(nodeId);
                    continue;
                }

                foreach (KeyValuePair<int, SubTreeEntry> pair in subtree)
                {
                    if (pair.Value.subnodes.Contains(nodeId))
                    {
                        childNodeIds.Add(nodeId);
                        break;
                    }
                }
            }




            //步骤3 将原始节点放到框的可用最左侧
            if(step<4)
                step = 3;
            int origIndex = nodeIdIndexMapping[origId];
            FrameNode origNode = lnodes[origIndex];
            Frame origFrame = frameIdIndexMapping[origNode.belongedFrameId];

            for (int i = origFrame.start; i <= origFrame.end; i++)
            {
                //如果框内有节点不是自己的子节点，则将请求转发到父节点，否则可能会分裂该框，造成不一致性
                if (!IsChild(lnodes[i].nodeId))
                    return null;
            }

            int leftIndex = origFrame.start;
            do
            {
                if ((lnodes[leftIndex].anchor == false || origId == lnodes[leftIndex].nodeId) && IsNodeAvailable(lnodes[leftIndex].nodeId))
                    break;
                leftIndex++;
            } while (leftIndex < origIndex);
            if (leftIndex < origIndex && IsNodeAvailable(lnodes[leftIndex].nodeId))
            {
                //swap
                SwapNodes(origIndex, leftIndex, lnodes, nodeIndexMapping, nodeIdIndexMapping);
                origIndex = nodeIdIndexMapping[origId];
                origNode = lnodes[origIndex];
                origFrame = frameIdIndexMapping[origNode.belongedFrameId];
                Console.WriteLine("swap nodes");
            }
            lnodes[leftIndex].anchor = true;
            DumpFrames(lframes, lnodes, frameIdIndexMapping, currentId);



            //如果本节点已在匿名组内，则直接返回。这里不在开始就处理是因为要将其置于框最左侧
            foreach (int c in subtree.Keys)
            {
                if (anonGroups.subUnavailAnonyNodes.ContainsKey(c + "-" + k)
                    && anonGroups.subUnavailAnonyNodes[c + "-" + k].Contains(origId))
                {
                    Console.WriteLine("READER{0} is already in {1}-group", origId, k);
                    Utility.CopyHashSet(newgroup, anonGroups.groups[k]);
                    goto DONE;
                }
            }

            //之前没有判断子树节点，是因为没有确定本节点是否已经在现有匿名组中了
            //自己子树的节点小于
            if (childNodeIds.Count < k)
                return null;

            //如果建组策略为严格的，那么将Ai所在全部匿名组的交集中全部元素为自由节点的框置于Ai所在框后
            //if (global.isBuildGroupPolicyStrict == true)
            //{

            //求Ai所在全部匿名组的交集Gmin(Ai)
            //如果存在Gmin(Ai)的子集构成一个框，且框内所有节点为自由的
            //检查交集中每一个框
            PrivacyReader origReader = (PrivacyReader)global.readers[origId];
            HashSet<int> insection = origReader.GetGroupInsection();
            //更正1：不是origNode的匿名组交集，而是其所在框的父框，因为该框和与之交换的框都不能跳出父框
            //检查父框中所有框，找出全部为自由节点的框，与其后的非全为自由节点的框交换

            //更正2：应该还是交集，新的组不能跳出这个交集外
            
            HashSet<Frame> checkFrames = new HashSet<Frame>();
            //foreach (int nodeId in insection)
            for (int nodeIndex = origFrame.leftbound; nodeIndex <= origFrame.rightbound; )
            {
                FrameNode node = lnodes[nodeIndex];
                //FrameNode node = lnodes[nodeIdIndexMapping[nodeId]];

                if (!IsNodeAvailable(node.nodeId))
                {
                    nodeIndex++;
                    continue;
                }
                
                Frame frame = frameIdIndexMapping[node.belongedFrameId];

                //属于同一个框，返回
                if (node.belongedFrameId == origFrame.frameId)
                {
                    nodeIndex = frame.end + 1;
                    continue;
                }
                if (checkFrames.Contains(frame))
                    continue;
                checkFrames.Add(frame);
                bool isAllFreeFrame = IsAllFreeFrame(frame, lnodes);
                //如果该框全部由自由节点构成
                //则将该框与Ai所在框后的第一个非全自由节点的框交换
                //自由框可利用的前提是同在一个组内
                if (isAllFreeFrame == true && frame.leftbound==origFrame.leftbound && frame.rightbound == origFrame.rightbound)
                {
                    Frame nextNotAllFreeFrame = GetNextNotAllFreeFrame(origFrame, lnodes, lframes, frameIdIndexMapping, origFrame.rightbound);
                    //两框可交换的前提是其均在同一个组内
                    if (nextNotAllFreeFrame != null && nextNotAllFreeFrame != origFrame
                        && (frame.start - nextNotAllFreeFrame.start) * (nextNotAllFreeFrame.start - origFrame.start) > 0
                        && nextNotAllFreeFrame.leftbound == frame.leftbound && nextNotAllFreeFrame.rightbound == frame.rightbound)
                    {
                        SwapFrames(nextNotAllFreeFrame, frame, nodeIndexMapping, nodeIdIndexMapping, frameIdIndexMapping, lframes, lnodes);
                        origIndex = nodeIdIndexMapping[origId];
                        origNode = lnodes[origIndex];
                        origFrame = frameIdIndexMapping[origNode.belongedFrameId];
                        DumpFrames(lframes, lnodes, frameIdIndexMapping, currentId);
                    }

                    break;
                }
                nodeIndex = frame.end + 1;
            }
            /*}
            else//非严格，将前述交集的条件放宽到全集并执行
            {
                //TODO
            }*/

            //第三步从Ai开始先左后右依次选择k个元素生成新的框I，将其余元素组成新框II，如果被选择节点与Ai同在一个框内，则其匿名组是一致的，直接将其加入

            newgroup.Clear();
            int xi = origIndex;
            int start = xi;
            while (xi >= origFrame.start && newgroup.Count < k)
            {
                start = xi;
                if(IsNodeAvailable(lnodes[xi].nodeId))
                    newgroup.Add(lnodes[xi].nodeId);
                xi--;
            }
            //还不够，往右走
            xi = origIndex;
            int end = xi;
            while (xi <= origFrame.end && newgroup.Count < k)
            {
                end = xi;
                if (IsNodeAvailable(lnodes[xi].nodeId) && !newgroup.Contains(lnodes[xi].nodeId))
                    newgroup.Add(lnodes[xi].nodeId);
                xi++;
            }

            //如果在origFrame中还是没有足够的节点，但origFrame后面还有全部为自由节点的框，可与之组合成新组，则也加进来
            Frame lastFrame = null;
            lastFrame = frameIdIndexMapping[lnodes[end].belongedFrameId];
            for (int nodeIndex = origFrame.end + 1; nodeIndex<lnodes.Count&& newgroup.Count < k; )
            {
                Frame currentFrame = frameIdIndexMapping[lnodes[nodeIndex].belongedFrameId];
                //如果两个frame不在同一个父frame中，则放弃
                if (currentFrame.parent != lastFrame.parent)
                    break;
                //否则如果非全部是自由节点，放弃
                if (IsAllFreeFrame(currentFrame, lnodes) == false
                    //&& GetAvailFrameNodeCount(currentFrame, lnodes, k)+newgroup.Count<k
                    )
                    break;
                //否则可添加节点
                xi = nodeIndex;
                while (xi <= currentFrame.end && newgroup.Count < k)
                {
                    end = xi;
                    if (IsNodeAvailable(lnodes[xi].nodeId) && !newgroup.Contains(lnodes[xi].nodeId))
                        newgroup.Add(lnodes[xi].nodeId);
                    xi++;
                }
                lastFrame = currentFrame.parent;
                nodeIndex = currentFrame.end + 1;

            }

            //如果第三步不成功，说明原框中没有足够的可用节点，则进行第四步：选择有最多可用元素的框中的一个可用节点与Ai交换，重新执行第二步
            if (newgroup.Count < k)
            {
                if (step == 3)
                    goto STEP3FAIL;
                else if (step == 4)
                    goto STEP4FAIL;
            }

            //原始框最左边没有充满，新分出一个框来
            if (origFrame.start < start)
            {
                Frame newframe3 = new Frame(origFrame.start, start - 1);
                newframe3.leftbound = origFrame.start;
                newframe3.rightbound = origFrame.end;
                newframe3.parent = origFrame;
                for (int i = origFrame.start; i <= start - 1; i++)
                    lnodes[i].belongedFrameId = newframe3.frameId;
                lframes.Add(newframe3);
                lallframes.Add(newframe3);
            }

            //否则考察右边界
            Frame endframe = frameIdIndexMapping[lnodes[end].belongedFrameId];
            //如果在原框内，则原框分出两个框来
            if (origFrame.end > end)
            {

                Frame newframe1 = new Frame(start, end);
                newframe1.leftbound = origFrame.start;
                newframe1.rightbound = origFrame.end;
                newframe1.parent = origFrame;
                for (int i = newframe1.start; i <= newframe1.end; i++)
                    lnodes[i].belongedFrameId = newframe1.frameId;
                lframes.Add(newframe1);
                lallframes.Add(newframe1);

                Frame newframe2 = new Frame(end + 1, origFrame.end);
                FrameNode nnode = nodes[end];
                Frame nframe = frameIdIndexMapping[nnode.belongedFrameId];
                newframe2.leftbound = origFrame.start;
                newframe2.rightbound = nframe.rightbound;
                newframe2.parent = nframe;
                for (int i = newframe2.start; i <= newframe2.end; i++)
                    lnodes[i].belongedFrameId = newframe2.frameId;
                lframes.Add(newframe2);
                lallframes.Add(newframe2);

                lframes.Remove(origFrame);
            }
            else
            {
                //否则，原框不变，寻找右边界所在的框，需要新建两个子框


                //建立一个虚拟的框，作为新建框的父框
                Frame virtualFrame = new Frame(start, end);
                virtualFrame.leftbound = endframe.leftbound;
                virtualFrame.rightbound = endframe.rightbound;
                virtualFrame.parent = endframe.parent;
                lallframes.Add(virtualFrame);

                //找到虚拟框下面的框，该框需要改变结构
                foreach (Frame oframe in lallframes)
                {
                    //如果存在一个框的父节点为虚拟框的父节点，则将其置于虚拟框之下
                    if (oframe.parent!= null &&
                        oframe.parent.frameId == endframe.parent.frameId && oframe.frameId != endframe.frameId)
                    {
                        oframe.parent = virtualFrame;
                        oframe.leftbound = virtualFrame.start;
                        oframe.rightbound = virtualFrame.end;
                        break;
                    } 
                }


                //原框作为新框的子框，其边和边界需要变化
                for (int j = start; j < end;)
                {
                    FrameNode onode = nodes[j];
                    Frame oframe = frameIdIndexMapping[onode.belongedFrameId];

                    if (oframe.end > end)
                    {
                        //新纳入的框边界变成
                        Frame newframe1 = new Frame(oframe.start, end);
                        newframe1.leftbound = start;
                        newframe1.rightbound = end;
                        newframe1.parent = virtualFrame;
                        for (int i = newframe1.start; i <= newframe1.end; i++)
                            lnodes[i].belongedFrameId = newframe1.frameId;
                        lframes.Add(newframe1);
                        lallframes.Add(newframe1);

                        //剩下的框边界不变
                        Frame newframe2 = new Frame(end + 1, oframe.end);
                        newframe2.leftbound = endframe.leftbound;
                        newframe2.rightbound = endframe.rightbound;
                        newframe2.parent = endframe.parent;
                        for (int i = newframe2.start; i <= newframe2.end; i++)
                            lnodes[i].belongedFrameId = newframe2.frameId;
                        lframes.Add(newframe2);
                        lallframes.Add(newframe2);

                        lframes.Remove(oframe);
                        lallframes.Remove(oframe);

                    }
                    j = oframe.end + 1;
                }

            }
            

            
        DONE:
            //重新复制回去
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i] = lnodes[i];
            }
            DumpFrames(lframes, lnodes, currentId);

            this.frames = new HashSet<Frame>();
            this.allframes = new HashSet<Frame>();
            foreach(Frame frame in lframes)
                frames.Add(frame);
            foreach (Frame frame in lallframes)
                allframes.Add(frame);

            return newgroup;

        //如果第三步不成功，说明原框中没有足够的可用节点，则进行第四步
        STEP3FAIL:
            //第四步：选择有最多可用元素的框中的一个可用节点与Ai交换，重新执行第二步
            step = 4;
            Frame mostFreeNodeFrame = FindMostFreeNodeFrame(lnodes[origIndex], lframes);
            if (mostFreeNodeFrame == null)
                goto STEP4FAIL;
            int fnodeIndex = FindAFreeNode(mostFreeNodeFrame);
            swapList.Add(lnodes[fnodeIndex].nodeId);
            SwapNodes(origIndex, fnodeIndex, lnodes, nodeIndexMapping, nodeIdIndexMapping);
            origIndex = nodeIdIndexMapping[origId];
            DumpFrames(lframes, lnodes, frameIdIndexMapping, currentId);
            goto STEP2;

        //第四步不成功，说明Am没有足够多的可用节点构造k-匿名组
        STEP4FAIL:
            //第五步中将Ai退出所有组，将请求发送给Am的父节点
            step = 5;
            return null;
        }

        bool IsAllFreeFrame(Frame frame, List<FrameNode> lnodes)
        {
            bool isFreeFrame = true;
            for (int i = frame.start; i <= frame.end; i++)
            {
                FrameNode node1 = lnodes[i];
                //if (node1.anchor == true && IsNodeAvailable(node1.nodeId))
                if (node1.anchor == true)
                {
                    isFreeFrame = false;
                    break;
                }
            }
            return isFreeFrame;
        }
        
        int GetAvailFrameNodeCount(Frame frame, List<FrameNode> lnodes, int k)
        {
            int count = 0;
            for (int i = frame.start; i <= frame.end; i++)
            {
                if (IsNodeAvailable(i))
                    count++;
            }
            return count;
        }

        void SwapNodes(int i1, int i2,  List<FrameNode> lnodes, Dictionary<FrameNode, int> nodeIndexMapping, Dictionary<int, int> nodeIdIndexMapping)
        {
            //DumpNodes(lnodes);
            FrameNode tmp = lnodes[i1];
            lnodes[i1] = lnodes[i2];
            lnodes[i2] = tmp;
            int tmpf = lnodes[i1].belongedFrameId;
            lnodes[i1].belongedFrameId = lnodes[i2].belongedFrameId;
            lnodes[i2].belongedFrameId = tmpf;
            //DumpNodes(lnodes);
            nodeIndexMapping[lnodes[i1]] = i1;
            nodeIndexMapping[lnodes[i2]] = i2;
            nodeIdIndexMapping[lnodes[i1].nodeId] = i1;
            nodeIdIndexMapping[lnodes[i2].nodeId] = i2;
        }

        int FindAFreeNode(Frame frame)
        {
            for (int i = frame.start; i < frame.end; i++)
            {
                if (nodes[i].anchor == false)
                    return i;
            }
            return -1;
        }

        //找到非origId的框外自由节点最多的框
        Frame FindMostFreeNodeFrame(FrameNode origNode, HashSet<Frame> lframes)
        {
            Frame mframe = null;
            int mfncount = -1;
            foreach (Frame frame in lframes)
            {
                if (frame.frameId == origNode.belongedFrameId)
                    continue;

                if (mframe == null)
                {
                    mframe = frame;
                    mfncount = GetFreeNodes(frame);
                    continue;
                }
                int fncount = GetFreeNodes(frame);
                if (fncount > mfncount)
                {
                    mframe = frame;
                    mfncount = fncount;
                }                
            }
            return mframe;
        }

        int GetFreeNodes(Frame frame)
        {
            int fnodes = 0;
            for(int i = frame.start;i<frame.end;i++)
            {
                if(nodes[i].anchor == false)
                    fnodes ++;
            }
            return fnodes;
        }



        public void SwapFrames(Frame frame1, Frame frame2, 
            Dictionary<FrameNode, int> nodeIndexMapping, Dictionary<int, int> nodeIdIndexMapping, 
            Dictionary<int, Frame> frameIdIndexMapping, HashSet<Frame> lframes, List<FrameNode> lnodes)
        {
            if (frame1.frameId == frame2.frameId)
                return;
            Frame foreframe, backframe = null;
            if (frame1.start < frame2.start)
            {
                foreframe = frame1;
                backframe = frame2;
            }
            else
            {
                foreframe = frame2;
                backframe = frame1;
            }

            HashSet<Frame> checkedframes = new HashSet<Frame>();

            //先把两个frame腾出地方来
            List<FrameNode> oforeframe = new List<FrameNode>();
            List<FrameNode> obackframe = new List<FrameNode>();
            for (int i = foreframe.start; i <= foreframe.end; i++)
                oforeframe.Add(lnodes[i]);
            for (int i = backframe.start; i <= backframe.end; i++)
                obackframe.Add(lnodes[i]);
            int forestart = foreframe.start;
            int backend = backframe.end;

            int forecount = foreframe.end-foreframe.start;
            int backcount = backframe.end-backframe.start;
            if (forecount<backcount)
            {
                int offset = backcount - forecount;

                for (int i = backframe.start-1; i > foreframe.end; i--)
                {
                    lnodes[i + offset] = lnodes[i];
                    Frame framex = frameIdIndexMapping[lnodes[i].belongedFrameId];
                    if (!checkedframes.Contains(framex))
                    {
                        checkedframes.Add(framex);
                        framex.start = framex.start + offset;
                        framex.end = framex.end + offset;
                    }
                }
            }
            else
            {
                int offset = forecount - backcount;

                for (int i = foreframe.end+1; i < backframe.start; i++)
                {
                    lnodes[i-offset] = lnodes[i];
                    Frame framex = frameIdIndexMapping[lnodes[i].belongedFrameId];
                    if (!checkedframes.Contains(framex))
                    {
                        checkedframes.Add(framex);
                        framex.start = framex.start - offset;
                        framex.end = framex.end - offset;
                    }
                }
            }


            //DumpNodes();
            //将前面的框放到后面
            for (int i = backframe.end-forecount, j = 0; i <= backframe.end; i++, j++)
            {
                lnodes[i] = oforeframe[j];
            }
            //DumpNodes();
            //将后面的框放到前面
            for (int i = foreframe.start, j = 0; i <= foreframe.start+backcount; i++, j++)
            {
                lnodes[i] = obackframe[j];
            }
            //DumpNodes();


            //新的框的起始位置
            foreframe.start = backend - (oforeframe.Count - 1);
            foreframe.end = backend;
            backframe.start = forestart;
            backframe.end = forestart + (obackframe.Count - 1);

            //update indexes
            for (int i = 0; i < lnodes.Count; i++)
            {
                FrameNode tnode = lnodes[i];
                nodeIndexMapping[tnode] = i;
                nodeIdIndexMapping[tnode.nodeId] = i;
            }
                
        }


        public Frame GetNextNotAllFreeFrame(Frame frame, List<FrameNode> lnodes, HashSet<Frame> lframes, 
            Dictionary<int, Frame> frameIdIndexMapping, int rightbound)
        {
            if (frame.end + 1 >= lnodes.Count)
                return null;

            int leftbound = frame.end + 1;
            int currentindex = leftbound;
            while(currentindex<=rightbound)
            {
                FrameNode node = lnodes[currentindex];
                Frame nframe = frameIdIndexMapping[node.belongedFrameId];
                bool isAllFreeFrame = IsAllFreeFrame(frame, lnodes);
                if (!isAllFreeFrame)
                    return nframe;
                currentindex = nframe.end + 1;
                continue;
            }
            return null;
        }

    }

     

    class Step2Exception : Exception
    { 
    }

    public class Frame:ICloneable
    {
        //对每个框进行标记，currentFrameId为当前最大标记
        public static int currentFrameId = 0;

        public int frameId;
        public int start;
        public int end;

        public int leftbound;
        public int rightbound;

        public Frame parent;


        public Frame(int start, int end)
        {
            this.frameId = currentFrameId++;
            this.start = start;
            this.end = end;
            this.leftbound = start;
            this.rightbound = end;
            this.parent = null;
        }

        private Frame(int start, int end, int leftbound, int rightbound, Frame parent, int frameId)
        {
            this.frameId = frameId;
            this.start = start;
            this.end = end;
            this.leftbound = leftbound;
            this.rightbound = rightbound;
            this.parent = parent;
        }


        public object Clone()
        {
            Frame newFrame = new Frame(this.start, this.end, this.leftbound, this.rightbound, this.parent, this.frameId);
            return newFrame;
        }


    }

    public class FrameNode:ICloneable
    {
        public bool anchor = false;
        public int nodeId = -1;
        public int belongedFrameId = -1;

        public FrameNode(int nodeId, bool anchor, int belongedFrameId)
        {
            this.nodeId = nodeId;
            this.anchor = anchor;
            this.belongedFrameId = belongedFrameId;
        }




        public object Clone()
        {
            FrameNode newNode = new FrameNode(this.nodeId, this.anchor, this.belongedFrameId);
            return newNode;
        }
    }

}
