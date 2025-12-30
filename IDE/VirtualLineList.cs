using System;
using System.Collections;
using System.Collections.Generic;

namespace IDE
{
    public class VirtualLineList : System.Collections.Generic.IList<string>, System.Collections.IList
    {
        private int _count = 0;

        public void Reload()
        {
            _count = (int)NativeMethods.get_line_count();
        }

        public string this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) return "";
                IntPtr ptr = NativeMethods.get_line_content((UIntPtr)index);
                return NativeMethods.GetStringFromPtr(ptr) ?? "";
            }
            set { }
        }

        public int Count => _count;
        public bool IsReadOnly => true;

        public void Add(string i) { }
        public void Clear() { }
        public bool Contains(string i) => false;
        public void CopyTo(string[] a, int i) { }
        public System.Collections.Generic.IEnumerator<string> GetEnumerator() { yield break; }
        public int IndexOf(string i) => -1;
        public void Insert(int i, string x) { }
        public bool Remove(string i) => false;
        public void RemoveAt(int i) { }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { yield break; }
        bool System.Collections.ICollection.IsSynchronized => false;
        object System.Collections.ICollection.SyncRoot => this;
        bool System.Collections.IList.IsFixedSize => true;
        bool System.Collections.IList.IsReadOnly => true;

        object System.Collections.IList.this[int index]
        {
            get => this[index];
            set { }
        }

        int System.Collections.IList.Add(object v) => -1;
        void System.Collections.IList.Clear() { }
        bool System.Collections.IList.Contains(object v) => false;
        int System.Collections.IList.IndexOf(object v) => -1;
        void System.Collections.IList.Insert(int i, object v) { }
        void System.Collections.IList.Remove(object v) { }
        void System.Collections.IList.RemoveAt(int i) { }
        void System.Collections.ICollection.CopyTo(Array a, int i) { }
    }
}