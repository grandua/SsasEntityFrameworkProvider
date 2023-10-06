using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgileDesign.Utilities;

namespace AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration
{
    class ColumnOrderTracker
    {
        //Column indexes are 0-based
        int linqColumnIndex;
        int columnsAxisColumnIndex;
        public int ColumnsAxisColumnIndex
        {
            get { return columnsAxisColumnIndex; }
        }
        int rowsAxisColumnIndex;
        public int RowsAxisColumnIndex
        {
            get { return rowsAxisColumnIndex; }
        }
        Dictionary<int, Func<int>> linqToMdxColumnsOrder_Delayed;
        IDictionary<int, Func<int>> LinqToMdxColumnsOrder_Delayed
        {
            get { return Init.InitIfNull(ref linqToMdxColumnsOrder_Delayed); }
        }

        public IDictionary<int, int> LinqToMdxColumnsOrder
        {
            get
            {
                return LinqToMdxColumnsOrder_Delayed
                    .ToDictionary(i => i.Key, i => i.Value());
            }
        }

        public void UpdateColumnsAxisColumnOrder()
        { //TODO: move into SelectStatement and get rid of if(!IsTopMost)
            int currentColumnsAxisColumnIndex = ColumnsAxisColumnIndex;
            LinqToMdxColumnsOrder_Delayed[linqColumnIndex]
                = (() => RowsAxisColumnIndex + currentColumnsAxisColumnIndex);
            columnsAxisColumnIndex = ColumnsAxisColumnIndex + 1;

            linqColumnIndex = linqColumnIndex + 1;
        }

        public void UpdateRowsAxisColumnOrder()
        {
            int currentRowsAxisColumnIndex = RowsAxisColumnIndex;
            LinqToMdxColumnsOrder_Delayed[linqColumnIndex]
                = (() => currentRowsAxisColumnIndex);
            rowsAxisColumnIndex = RowsAxisColumnIndex + 1;

            linqColumnIndex = linqColumnIndex + 1;
        }

        public override string ToString()
        {
            return ( (IDictionary)LinqToMdxColumnsOrder ).ToDisplayString();
        }
    }
}