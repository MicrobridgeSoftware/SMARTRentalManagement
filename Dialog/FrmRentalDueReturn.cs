using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Telerik.WinControls;
using Telerik.WinControls.Data;

namespace SMARTRentalManagement.Dialog
{
    public partial class FrmRentalDueReturn : Telerik.WinControls.UI.RadForm
    {
        private List<SaleTransactionDeliveryView> saleTransactionDeliveries;

        public FrmRentalDueReturn()
        {
            InitializeComponent();
        }

        public FrmRentalDueReturn(List<SaleTransactionDeliveryView> saleTransactionDeliveries) : this()
        {
            this.saleTransactionDeliveries = saleTransactionDeliveries;
        }

        private void FrmRentalDueReturn_Load(object sender, EventArgs e)
        {
            GroupDescriptor groupDescriptor = new GroupDescriptor();
            groupDescriptor.GroupNames.Add("CustomerName", ListSortDirection.Ascending);
            this.grdDataDisplay.GroupDescriptors.Add(groupDescriptor);
            grdDataDisplay.MasterTemplate.ExpandAllGroups();

            grdDataDisplay.DataSource = saleTransactionDeliveries;
        }
    }
}