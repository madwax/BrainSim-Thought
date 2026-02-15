/*
 * Brain Simulator Thought
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Thought and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */
using static UKS.UKS;

namespace UKS;

public class Link : Thought
{
    public Link() { }
    public Link(Thought from, Thought linkType, Thought to)
    {
        From = from;
        LinkType = linkType;
        To = to;
    }

    public Thought? From { get; set; }
    public Thought? LinkType { get; set; }
    public Thought? To { get; set; }
}

/// <summary>
/// A Thought is an atomic unit of thought. In the lexicon of graphs, a Thought is both a "node" and an Edge.  
/// A Thought can represent anything, physical object, attribute, word, action, feeling, etc.
/// </summary>
public class Thought
{
    public static Thought IsA { get => ThoughtLabels.GetThought("is-a"); }  //this is a cache value shortcut for (Thought)"is-a"
    private readonly List<Link> _linksTo = new();   // links to "has", "is", is-a, many others
    private readonly List<Link> _linksFrom = new(); // links from

    /// <summary>Unsafe writeable list of outgoing links.</summary>
    public List<Link> LinksToWriteable { get => _linksTo; }
    /// <summary>Unsafe writeable list of incoming links.</summary>
    public List<Link> LinksFromWriteable { get => _linksFrom; }
    /// <summary>Safe snapshot of outgoing links.</summary>
    public IReadOnlyList<Link> LinksTo { get { lock (_linksTo) { return new List<Link>(_linksTo.AsReadOnly()); } } }
    /// <summary>Safe snapshot of incoming links.</summary>
    public IReadOnlyList<Link> LinksFrom { get { lock (_linksFrom) { return new List<Link>(_linksFrom.AsReadOnly()); } } }
    /// <summary>Direct parents (targets of outgoing is-a links).</summary>
    public IReadOnlyList<Thought> Parents
    {
        get
        {
            lock (_linksTo)
                return _linksTo.Where(x => x.LinkType?.Label == "is-a")
                               .Select(x => x.To)
                               .Where(x => x is not null)!
                               .ToList();
        }
    }
    /// <summary>Direct children (sources of incoming is-a links).</summary>
    public IReadOnlyList<Thought> Children
    {
        get
        {
            lock (_linksFrom)
                return _linksFrom.Where(x => x.LinkType?.Label == "is-a")
                                 .Select(x => x.From)
                                 .Where(x => x is not null)!
                                 .ToList();
        }
    }

    private string _label = "";
    public string Label
    {
        get => _label;
        set
        {
            if (value == _label) return;
            ThoughtLabels.RemoveThoughtLabel(_label);
            _label = ThoughtLabels.AddThoughtLabel(value, this);
        }
    }

    public int UseCount = 0;
    public DateTime LastFiredTime = DateTime.Now;

    private TimeSpan _timeToLive = TimeSpan.MaxValue;
    public TimeSpan TimeToLive
    {
        get => _timeToLive;
        set
        {
            _timeToLive = value;
            if (_timeToLive != TimeSpan.MaxValue)
                AddToTransientList();
        }
    }

    private object _value;
    /// <summary>Any serializable object can be attached to a Thought. ONLY STRINGS are supported for save/restore to disk file.</summary>
    public object V
    {
        get => _value;
        set { _value = value; }
    }

    private float _weight = 1;
    /// <summary>Weight of this Thought (for links, applies to the link).</summary>
    public float Weight
    {
        get => _weight;
        set
        {
            _weight = value;
            if (this is Link link && link.LinkType?.HasProperty("IsCommutative") == true)
            {
                Link rReverse = link.To?.LinksTo.FindFirst(x => x.LinkType == link.LinkType && x.To == link.From);
                if (rReverse is not null)
                    rReverse._weight = _weight;
            }
        }
    }

    public Thought() { }

    public Thought(Thought r)
    {
        // Copy only common fields; link-specific fields handled via Link subclass.
        if (r is Link l)
        {
            // If this copy should be a link, caller should instantiate Link instead.
            return;
        }
        Weight = r.Weight;
        V = r.V;
        Label = r.Label;
    }

    public override string ToString()
    {
        string retVal = Label;
        if (this is Link lnk)
        {
            if (theUKS.IsSequenceElement(lnk))
            {
                var valuList = theUKS.FlattenSequence(lnk);
                retVal = "^" + string.Join("", valuList);
                return retVal;
            }

            retVal += "[();";
            if (lnk.From is not null) retVal += lnk.From.ToString();
            if (lnk.LinkType is not null) retVal += ((retVal == "") ? "" : "->") + lnk.LinkType.ToString();
            if (lnk.To is not null) retVal += ((retVal == "") ? "" : "->") + lnk.To.ToString();
            retVal += "]";
        }
        if (V is not null)
            retVal += "_V:" + V.ToString();

        return retVal;
    }

    public static implicit operator Thought(string label)
    {
        Thought t = ThoughtLabels.GetThought(label);
        return t;
    }

    public override bool Equals(object obj)
    {
        if (obj is Thought t)
        {
            if (Label != t.Label) return false;
            if (this is Link l1 && t is Link l2)
            {
                return l1.From == l2.From && l1.LinkType == l2.LinkType && l1.To == l2.To;
            }
            if (this is not Link && t is not Link)
                return true;
        }
        return false;
    }

    public static bool operator ==(Thought? a, Thought? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.Label != "" && a.Label == b.Label) return true;
        if (a is Link la && b is Link lb)
            return la.From == lb.From && la.To == lb.To && la.LinkType == lb.LinkType;
        return false;
    }
    public static bool operator !=(Thought? a, Thought? b) => !(a == b);

    public Thought AddDefaultLabel()
    {
        if (this is not Link l || l.LinkType is null) return this;
        if (string.IsNullOrEmpty(Label))
            Label = "R*";
        return this;
    }

    public IReadOnlyList<Thought> ChildrenWithSubclasses
    {
        get
        {
            List<Thought> retVal = Children.ToList();
            for (int i = 0; i < retVal.Count; i++)
            {
                Thought t = retVal[i];
                if (t.Label.StartsWith(this._label))
                {
                    retVal.AddRange(t.Children);
                    retVal.RemoveAt(i);
                    i--;
                }
            }
            return retVal;
        }
    }

    public IEnumerable<Thought> AncestorsWithSelf
    {
        get
        {
            yield return this;
            foreach (var ancestor in Ancestors)
                yield return ancestor;
        }
    }

    public IEnumerable<Thought> Ancestors
    {
        get
        {
            var queue = new Queue<Thought>(Parents);
            var seen = new HashSet<Thought>();

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (parent is null || !seen.Add(parent)) continue;

                yield return parent;

                foreach (var gp in parent.Parents)
                    queue.Enqueue(gp);
            }
        }
    }

    public IEnumerable<Thought> Descendants
    {
        get
        {
            var queue = new Queue<Thought>(Children);
            var seen = new HashSet<Thought>();

            while (queue.Count > 0)
            {
                var child = queue.Dequeue();
                if (child is null || !seen.Add(child)) continue;

                yield return child;

                foreach (var gc in child.Children)
                    queue.Enqueue(gc);
            }
        }
    }

    public bool HasAncestor(Thought t)
    {
        foreach (var ancestor in AncestorsWithSelf)
            if (ancestor == t) return true;
        return false;
    }

    public void Fire()
    {
        LastFiredTime = DateTime.Now;
    }

    // LINK OPERATIONS
    public Link AddLink(Thought target, Thought linkType)
    {
        if (linkType is null) return null;

        Link existing = HasLink(target, linkType);
        if (existing is not null)
            return existing;

        var r = new Link
        {
            LinkType = linkType,
            From = this,
            To = target,
        };
        if (target is not null && linkType is not null)
        {
            lock (_linksTo)
            lock (target._linksFrom)
            {
                LinksToWriteable.Add(r);
                target.LinksFromWriteable.Add(r);
            }
        }
        else
        {
            lock (_linksTo)
            {
                LinksToWriteable.Add(r);
            }
        }
        return r;
    }

    public void RemoveLinks(Thought linkType)
    {
        for (int i = 0; i < _linksTo.Count; i++)
        {
            Link r = _linksTo[i];
            if (r.From == this && r.LinkType == linkType)
            {
                RemoveLink(r);
                i--;
            }
        }
    }

    private Link HasLink(Thought target, Thought linkType)
    {
        foreach (Link r in _linksTo)
        {
            if (r.From == this && r.To == target && r.LinkType == linkType)
                return r;
        }
        return null;
    }

    public void RemoveLink(Link r)
    {
        if (r is null) return;
        if (r.LinkType is null) return;
        if (r.From is null)
        {
            lock (r.LinkType._linksFrom)
            {
                lock (r.To._linksFrom)
                {
                    r.LinkType._linksFrom.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To == r.To);
                    r.To._linksFrom.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To == r.To);
                }
            }
        }
        else if (r.To is null)
        {
            lock (r.From._linksTo)
            {
                lock (r.LinkType._linksFrom)
                {
                    r.From._linksTo.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To is null);
                    r.LinkType._linksFrom.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To is null);
                }
            }
        }
        else
        {
            lock (r.From._linksTo)
            {
                lock (r.LinkType._linksFrom)
                {
                    lock (r.To._linksFrom)
                    {
                        r.From._linksTo.Remove(r);
                        r.LinkType._linksFrom.Remove(r);
                        r.To._linksFrom.Remove(r);
                    }
                }
            }
        }
    }

    public Link HasLink(Thought source, Thought linkType, Thought targett)
    {
        if (source is null && linkType is null && targett is null) return null;
        foreach (Link r in LinksTo)
            if ((source is null || r.From == source) &&
                (linkType is null || r.LinkType == linkType) &&
                (targett is null || r.To == targett)) return r;
        return null;
    }

    public Link RemoveLink(Thought t2, Thought linkType)
    {
        Link r = new() { From = this, LinkType = linkType, To = t2 };
        RemoveLink(r);
        return r;
    }

    public Link AddParent(Thought newParent)
    {
        if (newParent is null) return null;
        if (!Parents.Contains(newParent))
            return AddLink(newParent, "is-a");
        return LinksTo.FindFirst(x => x.To == newParent && x.LinkType == IsA);
    }

    public void RemoveParent(Thought t)
    {
        Link r = new() { From = this, LinkType = IsA, To = t };
        t.RemoveLink(r);
    }

    public void RemoveChild(Thought t)
    {
        Link r = new() { From = t, LinkType = IsA, To = this };
        RemoveLink(r);
    }

    public List<Thought> GetAttributes()
    {
        List<Thought> retVal = new();
        foreach (Link r in LinksTo)
        {
            if (r.LinkType?.Label != "hasAttribute" && r.LinkType?.Label != "is") continue;
            retVal.Add(r.To);
        }
        return retVal;
    }

    public bool HasProperty(Thought t)  //with inheritance
    {
        if (t is null) return false;
        if (LinksTo.FindFirst(x => x.LinkType?.Label == "hasProperty" && x.To == t) is not null) return true;

        foreach (Thought t1 in Ancestors)
            if (t1.LinksTo.FindFirst(x => x.LinkType?.Label == "hasProperty" && x.To == t) is not null) return true;
        return false;
    }

    private void AddToTransientList()
    {
        if (!UKS.transientLinks.Contains(this))
            UKS.transientLinks.Add(this);
    }

    public IEnumerable<Thought> EnumerateSubThoughts()
    {
        var visited = new HashSet<Thought>();
        var q = new Queue<Thought>();

        void EnqueueIfNew(Thought? t)
        {
            if (t is null) return;
            if (visited.Add(t))
                q.Enqueue(t);
        }

        EnqueueIfNew(this);
        foreach (var isaLink in this.LinksTo.Where(x => x.LinkType?.Label == "is-a"))
            yield return isaLink;

        while (q.Count > 0)
        {
            var t = q.Dequeue();
            if (t is null) continue;

            if (t is Link lnk)
            {
                yield return lnk;
                EnqueueIfNew(lnk.LinkType);
                EnqueueIfNew(lnk.To);
                foreach (var isaLink in lnk.LinksFrom.Where(x => x.LinkType?.Label == "is-a"))
                {
                    EnqueueIfNew(isaLink);
                    EnqueueIfNew(isaLink.From);
                }
                foreach (var link in lnk.LinksTo.Where(x => x.LinkType?.Label != "is-a"))
                {
                    EnqueueIfNew(link);
                    EnqueueIfNew(link.To);
                }
                EnqueueIfNew(lnk.LinkType);
                EnqueueIfNew(lnk.To);
            }
        }
    }
}
