using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

namespace AgileDesign.SsasEntityFrameworkProvider.AdomdClient
{
    public class SsasParameterCollection 
        : DbParameterCollection, IList<SsasParameter>
    {
        readonly AdomdParameterCollection storeParameters;

        public SsasParameterCollection(AdomdParameterCollection storeParameters)
        {
            this.storeParameters = storeParameters;
        }

        public override int Add(object value)
        {
            return storeParameters.IndexOf(
                storeParameters.Add(((SsasParameter)value).StoreParameter));
        }

        public override bool Contains(object value)
        {
            return storeParameters.Contains(((SsasParameter)value).StoreParameter);
        }

        public void Add(SsasParameter item)
        {
            storeParameters.Add(item.StoreParameter);
        }

        public override void Clear()
        {
            storeParameters.Clear();
        }

        public bool Contains(SsasParameter item)
        {
            return storeParameters.Contains(item.StoreParameter);
        }

        public void CopyTo(SsasParameter[] array,
                           int arrayIndex)
        {
            array = this.ToArray();
        }

        public bool Remove(SsasParameter item)
        {
            storeParameters.Remove(item.StoreParameter);
            return true;
        }

        public override int IndexOf(object value)
        {
            Contract.Requires<ArgumentException>(value is SsasParameter);

            return IndexOf((SsasParameter)value);
        }

        public override void Insert(int index,
                                    object value)
        {
            Insert(index, (SsasParameter)value);
        }


        public override void Remove(object value)
        {
            storeParameters.Remove(((SsasParameter)value).StoreParameter);
        }

        public int IndexOf(SsasParameter item)
        {
            return storeParameters.IndexOf(item.StoreParameter);
        }

        public void Insert(int index,
                           SsasParameter item)
        {
            Insert(index, (object)item);
        }

        public override void RemoveAt(int index)
        {
            storeParameters.RemoveAt(index);
        }

        public new SsasParameter this[int index]
        {
            get { return (SsasParameter)base[index]; }
            set { base[index] = value; }
        }

        public override void RemoveAt(string parameterName)
        {
            storeParameters.RemoveAt(parameterName);
        }

        protected override void SetParameter(int index,
                                             DbParameter value)
        {
            storeParameters[index] = ((SsasParameter)value).StoreParameter;
        }

        protected override void SetParameter(string parameterName,
                                             DbParameter value)
        {
            storeParameters[parameterName] = ( (SsasParameter)value ).StoreParameter;
        }

        public override int Count
        {
            get { return storeParameters.Count; }
        }

        public SsasParameterCollection()
        {
            storeParameters = ( new AdomdCommand() ).Parameters;
        }

        object syncRoot;
        public override object SyncRoot
        {
            get
            {
                return syncRoot 
                       ?? ( syncRoot = new object() );
            }
        }

        public override bool IsFixedSize
        {
            get { return false; }
        }

        public override bool IsReadOnly
        {
            get { return false; }
        }

        public override bool IsSynchronized
        {
            get { return false; }
        }

        public override int IndexOf(string parameterName)
        {
            return storeParameters.IndexOf(parameterName);
        }

        IEnumerator<SsasParameter> IEnumerable<SsasParameter>.GetEnumerator()
        {
            return storeParameters.Cast<AdomdParameter>().Select(p => new SsasParameter(p)).GetEnumerator();
        }

        public override IEnumerator GetEnumerator()
        {
            return ((IEnumerable<SsasParameter>)this).GetEnumerator();
        }

        protected override DbParameter GetParameter(int index)
        {
            return new SsasParameter(storeParameters[index]);
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            return new SsasParameter(storeParameters[parameterName]);
        }

        public override bool Contains(string value)
        {
            return storeParameters.Contains(value);
        }

        public override void CopyTo(Array array,
                                    int index)
        {
            throw new NotImplementedException();
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                storeParameters.Add(((SsasParameter)value).StoreParameter);
            }
        }
    }
}