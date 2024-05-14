using ApplicationSecurity;
using ApplicationSecurity.PerformanceUtils;
using ApplicationSecurity.SecurityUtils;
using ApplicationSecurity.UI.Forms;
using ApplicationSecurity.UI.Forms.Company;
using iRMA.ApplicationGUI.Dashboard;
using iRMA.ApplicationGUI.Maintenance.Customer;
using iRMA.ApplicationGUI.Maintenance.General;
using iRMA.ApplicationGUI.Maintenance.Inventory;
using iRMA.ApplicationGUI.Maintenance.Inventory.Transactions;
using iRMA.ApplicationGUI.POS;
using iRMA.ApplicationGUI.ReportForms;
using iRMA.ApplicationGUI.ReportForms.InventoryReports;
using iRMA.ApplicationGUI.ReportForms.PosReports;
using SMARTRentalManagement.Dialog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Telerik.WinControls;
using Telerik.WinControls.UI;

namespace SMARTRentalManagement
{
    public partial class MainWindow : Telerik.WinControls.UI.RadForm
    {
        CurrentUserCredentials _credentials = new CurrentUserCredentials();
        private SystemSecurityConfiguration _securityConfig;
        List<SaleTransactionDeliveryView> saleTransactionDeliveries;
        RentalManagementDBEntities dbContext = new RentalManagementDBEntities();

        public MainWindow()
        {
            InitializeComponent();
            ThemeResolutionService.ApplicationThemeName = "Default";//"Office2010Blue";        
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            commandBarStripElement1.OverflowButton.Visibility = ElementVisibility.Collapsed;

            lblUser.Text = string.Empty;
            lblLocation.Text = string.Empty;
            lblDate.Text = string.Empty;
            lblTime.Text = string.Empty;

            bgwTheme.RunWorkerAsync();

            UserLoginForm _login = new UserLoginForm(_credentials);
            _login.ShowDialog();

            int _userValueCount = _credentials._userCredentials().Count;

            if (_userValueCount == 0)
            {
                mnuGenMaintenance.Enabled = false;
                mnuCustMaintenance.Enabled = false;
                mnuInvMaintenance.Enabled = false;
                mnuSaleTransactions.Enabled = false;
                mnuSecurity.Enabled = false;
                mnuDashbordViews.Enabled = false;
                mnuSysReports.Enabled = false;
                return;
            }

            lblUser.Text = "CURRENTLY LOGGED IN : " + _credentials.getFirstName().Trim().ToUpper() + " " + _credentials.getLastName().Trim().ToUpper();
            lblLocation.Text = "LOCATION : " + _credentials._loggedInUserStoreName().Trim().ToUpper();
            lblDate.Text = "DATE LOGGED IN : " + DateTime.Today.ToLongDateString().ToUpper();
            lblTime.Text = "TIME LOGGED IN : " + DateTime.Now.ToShortTimeString();

            ApplicationSecurityConstants._activeUser = _credentials._loggedInUserName();
            ApplicationSecurityConstants._storeLocationId = _credentials._loggedInUserStoreId();
            ApplicationSecurityConstants._isLocationSpecific = _credentials._loggedInUserAccessSpan();

            bool userAlreadyLoggedInForTheDay = false;

            using (RentalManagementDBEntities DBContext = new RentalManagementDBEntities())
            {
                string user = ApplicationSecurityConstants._activeUser.Trim().ToUpper();

                var _getUserImage = DBContext.SystemUserSettings.Where(x => x.UserName.Trim().ToUpper().Equals(user)).FirstOrDefault();

                if (_getUserImage != null)
                {
                    Bitmap bmp2;
                    var bytes2 = (byte[])_getUserImage.MdiParentImage;
                    MemoryStream ms2 = new MemoryStream(bytes2);
                    bmp2 = new Bitmap(ms2);
                    this.BackgroundImage = bmp2;

                    ms2.Flush();
                    ms2.Dispose();
                }

                DateTime currentDate = DateTime.Today.Date;

                saleTransactionDeliveries = new List<SaleTransactionDeliveryView>();

                _securityConfig = DBContext.SystemSecurityConfigurations.AsNoTracking().FirstOrDefault();

                userAlreadyLoggedInForTheDay = DBContext.SysUserAuditViews.Where(x => x.UserName.Trim().ToUpper().Equals(user) &&
                                               DbFunctions.TruncateTime(x.LogInTime) == currentDate).AsNoTracking().Any();

                int storeId = ApplicationSecurityConstants._storeLocationId;

                saleTransactionDeliveries = DBContext.SaleTransactionDeliveryViews.Where(x => x.TransactionCategory.Trim().Equals("Rental")
                                            && DbFunctions.TruncateTime(x.ActualRentalReturnDueDate.Value) == currentDate
                                            && x.StoreLocationId == storeId && x.TotalReturnedQuantity == 0).AsNoTracking().ToList();
            }

            EnableUserControls();
            //configureCommandBar();
            timerAutoLogOut.Enabled = true;

            if (saleTransactionDeliveries.Count > 0 && !userAlreadyLoggedInForTheDay)
            {
                DialogResult promptUser = RadMessageBox.Show("There are rental items due for return today" + Environment.NewLine +
                                                             "Would you like to generate a list of these item(s)?", Application.ProductName,
                                                             MessageBoxButtons.YesNo, RadMessageIcon.Question, MessageBoxDefaultButton.Button1);

                if (promptUser == DialogResult.Yes)
                {
                    FrmRentalDueReturn rentalDueReturn = new FrmRentalDueReturn(saleTransactionDeliveries);
                    rentalDueReturn.ShowDialog();
                }
            }

            MemoryManagement.FlushMemory();
        }

        private void EnableUserControls()
        {
            foreach (RadMenuItem _menuItem in radMenu1.Items)
            {
                if (!_menuItem.Name.Trim().Equals("radMenuItem1")) //_menuItem.Items.Count == 0)//3 || _menuItem.Items.Count == 2)
                {
                    _menuItem.Enabled = false;

                    for (int i = 0; i < _menuItem.Items.Count; i++)
                    {
                        if (Convert.ToInt16(_menuItem.Items[i].Tag) != 1)
                            _menuItem.Items[i].Enabled = false;
                    }
                }
            }

            var _distinctMenu = _credentials._userCredentials().Select(x => x.getMenuName()).Distinct().ToList();

            foreach (var _value in _distinctMenu)
            {
                string _mainMenu = _value.ToString();

                var _sysMenu = radMenu1.Items.Where(x => x.Name.Equals(_mainMenu)).First();
                _sysMenu.Enabled = true;

                var _nodes = _credentials._userCredentials().Where(x => x.getMenuName().Equals(_mainMenu)).ToList();

                foreach (var node in _nodes)
                {
                    string _nodeName = node.getNodeName();

                    foreach (RadMenuItem _mainMenuName in radMenu1.Items)
                    {
                        if (_mainMenuName.Name.Equals(_sysMenu.Name))
                        {
                            for (int i = 0; i < _mainMenuName.Items.Count; i++)
                            {
                                if (_mainMenuName.Items[i].Name.Equals(_nodeName))
                                    _mainMenuName.Items[i].Enabled = true;
                            }
                        }
                    }
                }
            }
        }

        private void configureCommandBar()
        {
            foreach (CommandBarRowElement row in radCommandBar1.Rows)
            {
                foreach (CommandBarStripElement strip in row.Strips)
                {
                    foreach (RadCommandBarBaseItem item in strip.Items)
                    {
                        if (item is CommandBarButton && item.Name != "cmdExit" && item.Name != "cmdLogout" && item.Name != "cmdBackground")
                            item.Enabled = false;

                        if (item.Name.Equals("cmdInventory"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuInventoryItems")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdSuppliers"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuSupplier")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdCompanySettings"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuCompany")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdEmployee"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuEmployee")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdPayMethod"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuPayMethod")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdCustomers"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuCustomer")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdApplyCustPayment"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuApplyCustPayment")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdTransfers"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuBranchTransfer")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdMaintenance"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuServiceOrder")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdQuotation"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuSaleQuote")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdPOS"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuSaleTransaction")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdIssueItems"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuDelivery")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdVoids"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuRentalPayment")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdReprints"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuReprints")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdUserPrivileges"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuUserGroup")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdUser"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuAddUser")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                        else if (item.Name.Equals("cmdOrder"))
                        {
                            bool nodeAccessible = _credentials._userCredentials().Where(x => x.getNodeName().Equals("mnuOrder")).Any();

                            if (nodeAccessible)
                                item.Enabled = true;
                        }
                    }
                }
            }
        }

        private void mnuSysSecurity_Click(object sender, EventArgs e)
        {
            SystemSecurityConfigurationForm _configForm = new SystemSecurityConfigurationForm();
            _configForm.MdiParent = this;
            _configForm.Show();
        }

        private void mnuAddUser_Click(object sender, EventArgs e)
        {
            SystemUserForm _addSysUser = new SystemUserForm();
            _addSysUser.MdiParent = this;
            _addSysUser.Show();
        }

        private void mnuUserPassword_Click(object sender, EventArgs e)
        {
            SystemUserDisplayForm _changePassword = new SystemUserDisplayForm();
            _changePassword.MdiParent = this;
            _changePassword.Show();
        }

        private void mnuSysAudit_Click(object sender, EventArgs e)
        {
            SystemAuditTrailForm _systemAudit = new SystemAuditTrailForm();
            _systemAudit.MdiParent = this;
            _systemAudit.Show();
        }

        private void mnuUserAudit_Click(object sender, EventArgs e)
        {
            SystemUserLoginAudit _userAudit = new SystemUserLoginAudit();
            _userAudit.MdiParent = this;
            _userAudit.Show();
        }

        private void timerAutoLogOut_Tick(object sender, EventArgs e)
        {
            if (_securityConfig != null && _securityConfig.EnableAutoLogOut == true)
            {
                int autoLogoutTimeInSeconds = _securityConfig.AutoLogOutMinutes * 60;
                uint lastInputTime = EnvironmentUtils.GetLastInputTime();

                if (ApplicationSecurityConstants._activeUser != null && lastInputTime >= autoLogoutTimeInSeconds)
                    PerformLogOut();
            }

            if (saleTransactionDeliveries.Count > 0)
            {
                //DesktopAlertManager manager;
                //manager.se

                //var manager = new RadDesktopAlertManager();
                //manager = new RadDesktopAlertManager(AlertScreenPosition.BottomCenter);
                //manager = new RadDesktopAlertManager(AlertScreenPosition.TopCenter, 5);
                //manager = new RadDesktopAlertManager(AlertScreenPosition.TopRight, new Point(0, 0));
                //manager = new RadDesktopAlertManager(AlertScreenPosition.BottomCenter, new Point(0, 0), 10);

                string currentTimeString = DateTime.Now.ToString("hh:mm:ss");

                foreach (SaleTransactionDeliveryView transactions in saleTransactionDeliveries)
                {
                    bool itemReturned = dbContext.SaleTransactionDeliveryViews.Where(x => x.SaleTransactionDeliveryDetailId == transactions.SaleTransactionDeliveryDetailId && x.TotalReturnedQuantity > 0).Any();

                    if (transactions.ActualRentalReturnDueDate.Value.ToString("hh:mm:ss").Equals(currentTimeString) && !itemReturned)
                    {
                        RadDesktopAlert DesktopAlert = new RadDesktopAlert();

                        DesktopAlert.ContentImage = Properties.Resources.information2;

                        DesktopAlert.ScreenPosition = AlertScreenPosition.BottomRight;

                        DesktopAlert.ShowOptionsButton = false;

                        DesktopAlert.AutoClose = false;

                        DesktopAlert.CanMove = true;

                        DesktopAlert.CaptionText = "Rental Returns";

                        if (transactions.RequirePickup)
                            DesktopAlert.ContentText = "Rental transaction " + transactions.TransactionNo.Trim() + " is now due for return." +
                                                       "Your assistance is required for the pick-up of these item(s). " +
                                                       transactions.CustomerName.Trim() + " may be contacted at " + transactions.ContactNo1.Trim();

                        else
                            DesktopAlert.ContentText = "Rental transaction " + transactions.TransactionNo.Trim() + " is now due for return." +
                                                       transactions.CustomerName.Trim() + " is responsible for the return of these item(s). " +
                                                       transactions.CustomerName.Trim() + " may be contacted at " + transactions.ContactNo1.Trim();


                        DesktopAlert.Popup.AlertElement.CaptionElement.TextAndButtonsElement.TextElement.ForeColor = Color.Red;

                        DesktopAlert.Popup.AlertElement.CaptionElement.CaptionGrip.BackColor = Color.Red;

                        DesktopAlert.Popup.AlertElement.CaptionElement.CaptionGrip.GradientStyle = GradientStyles.Solid;

                        DesktopAlert.Popup.AlertElement.ContentElement.Font = new Font("Arial", 9.5f, FontStyle.Italic);

                        DesktopAlert.Popup.AlertElement.ContentElement.TextImageRelation = TextImageRelation.TextBeforeImage;

                        DesktopAlert.Popup.AlertElement.BackColor = Color.Yellow;

                        DesktopAlert.Popup.AlertElement.GradientStyle = GradientStyles.Solid;

                        DesktopAlert.Popup.AlertElement.BorderColor = Color.Red;

                        DesktopAlert.Show();
                    }
                }
            }
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            using (RentalManagementDBEntities dbContext = new RentalManagementDBEntities())
            {
                if (ApplicationSecurityConstants._activeUser != null)
                {
                    string user = ApplicationSecurityConstants._activeUser.Trim().ToUpper();

                    var _updateUserLoginStatus = dbContext.SystemUsers.Where(x => x.UserName.Trim().ToUpper().Equals(user)).FirstOrDefault();

                    if (_updateUserLoginStatus != null)
                    {
                        _updateUserLoginStatus.CurrentlyLoggedIn = false;
                        dbContext.SaveChanges();
                    }
                }
            }

            MemoryManagement.FlushMemory();
        }

        private void PerformLogOut()
        {
            for (int index = Application.OpenForms.Count - 1; index >= 0; index--)
            {
                if (Application.OpenForms[index].Name != "MainWindow")
                    Application.OpenForms[index].Close();
            }

            lblUser.Text = string.Empty;
            lblLocation.Text = string.Empty;
            lblDate.Text = string.Empty;
            lblTime.Text = string.Empty;

            using (RentalManagementDBEntities dbContext = new RentalManagementDBEntities())
            {
                if (ApplicationSecurityConstants._activeUser != null)
                {
                    string user = ApplicationSecurityConstants._activeUser.Trim().ToUpper();

                    var _updateUserLoginStatus = dbContext.SystemUsers.Where(x => x.UserName.Trim().ToUpper().Equals(user)).FirstOrDefault();

                    if (_updateUserLoginStatus != null)
                    {
                        _updateUserLoginStatus.CurrentlyLoggedIn = false;
                        dbContext.SaveChanges();
                    }
                }
            }

            if (this.BackgroundImage != null)
                this.BackgroundImage = null;

            timerAutoLogOut.Enabled = false;

            _credentials = new CurrentUserCredentials();

            UserLoginForm _login = new UserLoginForm(_credentials);
            _login.ShowDialog();

            int _userValueCount = _credentials._userCredentials().Count;

            if (_userValueCount == 0)
            {
                mnuGenMaintenance.Enabled = false;
                mnuCustMaintenance.Enabled = false;
                mnuInvMaintenance.Enabled = false;
                mnuSaleTransactions.Enabled = false;
                mnuSecurity.Enabled = false;
                mnuDashbordViews.Enabled = false;
                mnuSysReports.Enabled = false;
                return;
            }

            lblUser.Text = "CURRENTLY LOGGED IN : " + _credentials.getFirstName().Trim().ToUpper() + " " + _credentials.getLastName().Trim().ToUpper();
            lblLocation.Text = "LOCATION : " + _credentials._loggedInUserStoreName().Trim().ToUpper();
            lblDate.Text = "DATE LOGGED IN : " + DateTime.Today.ToLongDateString().ToUpper();
            lblTime.Text = "TIME LOGGED IN : " + DateTime.Now.ToShortTimeString();

            ApplicationSecurityConstants._activeUser = _credentials._loggedInUserName();
            ApplicationSecurityConstants._storeLocationId = _credentials._loggedInUserStoreId();
            ApplicationSecurityConstants._isLocationSpecific = _credentials._loggedInUserAccessSpan();

            GetUserSettingsForLogin();

            EnableUserControls();
            //configureCommandBar();
            timerAutoLogOut.Enabled = true;
        }

        private void GetUserSettingsForLogin()
        {
            using (RentalManagementDBEntities DBContext = new RentalManagementDBEntities())
            {
                string user = ApplicationSecurityConstants._activeUser.Trim().ToUpper();

                var _getUserImage = DBContext.SystemUserSettings.Where(x => x.UserName.Trim().ToUpper().Equals(user)).FirstOrDefault();

                if (_getUserImage != null)
                {
                    Bitmap bmp2;
                    var bytes2 = (byte[])_getUserImage.MdiParentImage;
                    MemoryStream ms2 = new MemoryStream(bytes2);
                    bmp2 = new Bitmap(ms2);
                    this.BackgroundImage = bmp2;

                    ms2.Flush();
                    ms2.Dispose();
                }
            }

            MemoryManagement.FlushMemory();
        }


        private void cmdCompanySettings_Click(object sender, EventArgs e)
        {
            CompanyForm _company = new CompanyForm();
            _company.MdiParent = this;
            _company.Show();
        }

        private void mnuUserGroup_Click(object sender, EventArgs e)
        {
            SystemUserGroupForm _systemUserGroup = new SystemUserGroupForm();
            _systemUserGroup.MdiParent = this;
            _systemUserGroup.Show();
        }

        private void mnuCusClassification_Click(object sender, EventArgs e)
        {
            FrmCustomerClassification _customerClassification = new FrmCustomerClassification();
            _customerClassification.MdiParent = this;
            _customerClassification.Show();
        }

        private void mnuCustType_Click(object sender, EventArgs e)
        {
            FrmCustomerType _customerType = new FrmCustomerType();
            _customerType.MdiParent = this;
            _customerType.Show();
        }

        private void mnuCreditTerm_Click(object sender, EventArgs e)
        {
            FrmCreditTerms _creditTerms = new FrmCreditTerms();
            _creditTerms.MdiParent = this;
            _creditTerms.Show();
        }

        private void mnuIdTypes_Click(object sender, EventArgs e)
        {
            FrmCustomerIdTypes _customerIdTypes = new FrmCustomerIdTypes();
            _customerIdTypes.MdiParent = this;
            _customerIdTypes.Show();
        }

        private void mnuCustomer_Click(object sender, EventArgs e)
        {
            FrmCustomerMaintenance _customerMaintenance = new FrmCustomerMaintenance();
            _customerMaintenance.MdiParent = this;
            _customerMaintenance.Show();
        }

        private void cmdBackground_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.png) | *.jpg; *.jpeg; *.jpe; *.png";

            if (openFileDialog.ShowDialog(this.Parent) == DialogResult.OK)
            {
                byte[] data;
                Bitmap bmp;

                string fileName = openFileDialog.FileName;

                FileStream fs = File.OpenRead(fileName);
                data = new byte[fs.Length];
                fs.Read(data, 0, data.Length);

                MemoryStream ms = new MemoryStream(data);
                bmp = new Bitmap(ms);
                this.BackgroundImage = bmp;

                DialogResult _defaultImage = RadMessageBox.Show("Do you want to continuously use this image as your Wallpaper?",
                                             Application.ProductName, MessageBoxButtons.YesNo);

                if (_defaultImage == System.Windows.Forms.DialogResult.Yes)
                {
                    using (RentalManagementDBEntities dbContext = new RentalManagementDBEntities())
                    {
                        string user = ApplicationSecurityConstants._activeUser.Trim().ToUpper();

                        var _userImage = dbContext.SystemUserSettings.Where(x => x.UserName.Trim().ToUpper().Equals(user)).FirstOrDefault();

                        if (_userImage == null)
                        {
                            SystemUserSetting _saveBackgroundImage = new SystemUserSetting();
                            _saveBackgroundImage.MdiParentImage = data;
                            _saveBackgroundImage.UserName = ApplicationSecurityConstants._activeUser;
                            dbContext.SystemUserSettings.Add(_saveBackgroundImage);
                        }
                        else
                        {
                            _userImage.MdiParentImage = data;
                        }

                        dbContext.SaveChanges();
                    }
                }

                MemoryManagement.FlushMemory();
            }
        }

        private void cmdLogout_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lblUser.Text))
                return;

            DialogResult _verifyAction = RadMessageBox.Show("Do you want to logout?", Application.ProductName, MessageBoxButtons.YesNo);

            if (_verifyAction == System.Windows.Forms.DialogResult.Yes)
                PerformLogOut();
        }

        private void cmdExit_Click(object sender, EventArgs e)
        {
            DialogResult _verifyAction = RadMessageBox.Show("Are you sure you want to close this Application?", Application.ProductName, MessageBoxButtons.YesNo);

            if (_verifyAction == System.Windows.Forms.DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void mnuCompany_Click(object sender, EventArgs e)
        {
            CompanyForm _company = new CompanyForm();
            _company.MdiParent = this;
            _company.Show();
        }

        private void mnuCountry_Click(object sender, EventArgs e)
        {
            FrmCountry _country = new FrmCountry();
            _country.MdiParent = this;
            _country.Show();
        }

        private void mnuTax_Click(object sender, EventArgs e)
        {
            FrmTaxType _taxType = new FrmTaxType();
            _taxType.MdiParent = this;
            _taxType.Show();
        }

        private void mnuSMTP_Click(object sender, EventArgs e)
        {
            FrmSMTPSettings _sMTPSettings = new FrmSMTPSettings();
            _sMTPSettings.MdiParent = this;
            _sMTPSettings.Show();
        }

        private void mnuPricePoint_Click(object sender, EventArgs e)
        {
            FrmPricePoint _pricePoint = new FrmPricePoint();
            _pricePoint.MdiParent = this;
            _pricePoint.Show();
        }

        private void cmdCustomers_Click(object sender, EventArgs e)
        {
            FrmCustomerMaintenance _customerMaintenance = new FrmCustomerMaintenance();
            _customerMaintenance.MdiParent = this;
            _customerMaintenance.Show();
        }

        private void mnuCustStatus_Click(object sender, EventArgs e)
        {
            FrmCustomerStatus _customerStatus = new FrmCustomerStatus();
            _customerStatus.MdiParent = this;
            _customerStatus.Show();
        }

        private void mnuInvClassification_Click(object sender, EventArgs e)
        {
            FrmInventoryClassification _inventoryClassification = new FrmInventoryClassification();
            _inventoryClassification.MdiParent = this;
            _inventoryClassification.Show();
        }

        private void mnuAssetClass_Click(object sender, EventArgs e)
        {
            FrmAssetClass _assetClass = new FrmAssetClass();
            _assetClass.MdiParent = this;
            _assetClass.Show();
        }

        private void mnuInventoryLocation_Click(object sender, EventArgs e)
        {
            FrmInventoryLocation _inventoryLocation = new FrmInventoryLocation();
            _inventoryLocation.MdiParent = this;
            _inventoryLocation.Show();
        }

        private void mnuLocDepartment_Click(object sender, EventArgs e)
        {
            FrmLocationDepartment _locationDepartment = new FrmLocationDepartment();
            _locationDepartment.MdiParent = this;
            _locationDepartment.Show();
        }

        private void mnuUOM_Click(object sender, EventArgs e)
        {
            FrmUnitOfMeasure _unitOfMeasure = new FrmUnitOfMeasure();
            _unitOfMeasure.MdiParent = this;
            _unitOfMeasure.Show();
        }

        private void mnuInventoryItems_Click(object sender, EventArgs e)
        {
            FrmInventoryMaintenance _inventoryMaintenance = new FrmInventoryMaintenance();
            _inventoryMaintenance.MdiParent = this;
            _inventoryMaintenance.Show();
        }

        private void mnuBrands_Click(object sender, EventArgs e)
        {
            FrmItemBrand _itemBrand = new FrmItemBrand();
            _itemBrand.MdiParent = this;
            _itemBrand.Show();
        }

        private void mnuCurrency_Click(object sender, EventArgs e)
        {
            FrmCurrency _currency = new FrmCurrency();
            _currency.MdiParent = this;
            _currency.Show();
        }

        private void mnuItemSizes_Click(object sender, EventArgs e)
        {
            FrmItemSizes _itemSizes = new FrmItemSizes();
            _itemSizes.MdiParent = this;
            _itemSizes.Show();
        }

        private void mnuUserCode_Click(object sender, EventArgs e)
        {
            FrmUserCode _userCode = new FrmUserCode();
            _userCode.MdiParent = this;
            _userCode.Show();
        }

        private void mnuEmployee_Click(object sender, EventArgs e)
        {
            FrmEmployee _employee = new FrmEmployee();
            _employee.MdiParent = this;
            _employee.Show();
        }

        private void mnuSupplier_Click(object sender, EventArgs e)
        {
            FrmSupplier _supplier = new FrmSupplier();
            _supplier.MdiParent = this;
            _supplier.Show();
        }

        private void mnuPayMethod_Click(object sender, EventArgs e)
        {
            FrmPaymentMethod _paymentMethod = new FrmPaymentMethod();
            _paymentMethod.MdiParent = this;
            _paymentMethod.Show();
        }

        private void mnuInvReceival_Click(object sender, EventArgs e)
        {
            FrmInventoryReceival _inventoryReceival = new FrmInventoryReceival();
            _inventoryReceival.MdiParent = this;
            _inventoryReceival.Show();
        }

        private void mnuInvAdjustments_Click(object sender, EventArgs e)
        {
            FrmInventoryAdjustment _inventoryAdjustment = new FrmInventoryAdjustment();
            _inventoryAdjustment.MdiParent = this;
            _inventoryAdjustment.Show();
        }

        private void mnuPriceChange_Click(object sender, EventArgs e)
        {
            FrmItemPriceChange _itemPriceChange = new FrmItemPriceChange();
            _itemPriceChange.MdiParent = this;
            _itemPriceChange.Show();
        }

        private void mnuBranchTransfer_Click(object sender, EventArgs e)
        {
            FrmInterBranchTransfer _interBranchTransfer = new FrmInterBranchTransfer();
            _interBranchTransfer.MdiParent = this;
            _interBranchTransfer.Show();
        }

        private void mnuCountTypes_Click(object sender, EventArgs e)
        {
            FrmStockCountTypes _stockCountTypes = new FrmStockCountTypes();
            _stockCountTypes.MdiParent = this;
            _stockCountTypes.Show();
        }

        private void mnuServiceOrder_Click(object sender, EventArgs e)
        {
            FrmInventoryServiceOrder _serviceOrder = new FrmInventoryServiceOrder();
            _serviceOrder.MdiParent = this;
            _serviceOrder.Show();
        }

        private void mnuMaintenanceVoid_Click(object sender, EventArgs e)
        {
            FrmVoidServiceOrder _voidServiceOrder = new FrmVoidServiceOrder();
            _voidServiceOrder.MdiParent = this;
            _voidServiceOrder.Show();
        }

        private void mnuMaintenanceReturn_Click(object sender, EventArgs e)
        {
            FrmReturnServiceItem _returnServiceItem = new FrmReturnServiceItem();
            _returnServiceItem.MdiParent = this;
            _returnServiceItem.Show();
        }

        private void mnuEmailOption_Click(object sender, EventArgs e)
        {
            FrmEmailOptions _emailOptions = new FrmEmailOptions();
            _emailOptions.MdiParent = this;
            _emailOptions.Show();
        }

        private void mnuSaleQuote_Click(object sender, EventArgs e)
        {
            FrmQuotation _quotation = new FrmQuotation();
            _quotation.MdiParent = this;
            _quotation.Show();
        }

        private void mnuSaleTransaction_Click(object sender, EventArgs e)
        {
            FrmSaleTransaction _saleTransaction = new FrmSaleTransaction();
            _saleTransaction.MdiParent = this;
            _saleTransaction.Show();
        }

        private void mnuDelivery_Click(object sender, EventArgs e)
        {
            FrmItemDelivery _itemDelivery = new FrmItemDelivery();
            _itemDelivery.MdiParent = this;
            _itemDelivery.Show();
        }

        private void mnuRentReturns_Click(object sender, EventArgs e)
        {
            FrmRentalReturns _rentalReturns = new FrmRentalReturns();
            _rentalReturns.MdiParent = this;
            _rentalReturns.Show();
        }

        private void mnuReturnsDashboard_Click(object sender, EventArgs e)
        {
            FrmScheduledReturns _scheduledReturns = new FrmScheduledReturns();
            _scheduledReturns.MdiParent = this;
            _scheduledReturns.Show();
        }

        private void mnuHoliday_Click(object sender, EventArgs e)
        {
            FrmHoliday _holiday = new FrmHoliday();
            _holiday.MdiParent = this;
            _holiday.Show();
        }

        private void mnuReturnExclusion_Click(object sender, EventArgs e)
        {
            FrmReturnExclusion _returnExclusion = new FrmReturnExclusion();
            _returnExclusion.MdiParent = this;
            _returnExclusion.Show();
        }

        private void bgwTheme_DoWork(object sender, DoWorkEventArgs e)
        {
            var themefiles = Directory.GetFiles(System.Windows.Forms.Application.StartupPath, "Telerik.WinControls.Themes.*.dll");

            foreach (var theme in themefiles)
            {
                var themeAssembly = Assembly.LoadFile(theme);

                var themeType = themeAssembly.GetTypes().Where(_themes => typeof(RadThemeComponentBase).IsAssignableFrom(_themes)).FirstOrDefault();

                if (themeType != null)
                {
                    RadThemeComponentBase themeObject = (RadThemeComponentBase)Activator.CreateInstance(themeType);

                    if (themeObject != null)
                    {
                        themeObject.Load();
                    }
                }
            }

            int currentYear = DateTime.Now.Year;

            using (RentalManagementDBEntities dbContext = new RentalManagementDBEntities())
            {
                var getRecurringHolidays = dbContext.Holidays.Where(x => x.IsRecurringDate).AsNoTracking().ToList();

                if (getRecurringHolidays.Count > 0)
                {
                    List<string> holidayNameString = new List<string>();

                    holidayNameString = getRecurringHolidays.Select(x => x.HolidayName.Trim().ToUpper()).Distinct().ToList();

                    foreach (string name in holidayNameString)
                    {
                        bool currentYearHolidaysInserted = getRecurringHolidays.Where(x => x.HolidayName.Trim().ToUpper().Equals(name) && x.DateOfHoliday.Year == currentYear).Any();

                        if (!currentYearHolidaysInserted)
                        {
                            DateTime holidayDate = getRecurringHolidays.Where(x => x.HolidayName.Trim().ToUpper().Equals(name)).Select(x => x.DateOfHoliday).LastOrDefault();

                            Holiday addHoliday = new Holiday();
                            addHoliday.HolidayName = name;
                            addHoliday.IsRecurringDate = true;
                            addHoliday.DateOfHoliday = holidayDate.AddYears(1);
                            addHoliday.CreatedBy = "Admin"; //ApplicationSecurityConstants._activeUser;
                            addHoliday.DateCreated = DateTime.Now;
                            dbContext.Holidays.Add(addHoliday);

                            dbContext.SaveChanges();
                        }
                    }
                }
            }
        }

        private void bgwTheme_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var _themeList = ThemeRepository.AvailableThemeNames.ToList();
            string _defaultThemeName = string.Empty;

            if (_themeList.Count > 0)
            {
                cmdDdlTheme.Items.Add("Default");

                foreach (var _theme in _themeList)
                    cmdDdlTheme.Items.Add(_theme);

                cmdDdlTheme.MinSize = new Size(230, 20);

                _defaultThemeName = "Office2010Blue";
                cmdDdlTheme.SelectedIndex = cmdDdlTheme.Items.IndexOf(cmdDdlTheme.Items.First(x => x.Text == _defaultThemeName));
            }

            MemoryManagement.FlushMemory();
        }

        private void cmdDdlTheme_SelectedIndexChanged(object sender, Telerik.WinControls.UI.Data.PositionChangedEventArgs e)
        {
            RadDropDownListElement _element = sender as RadDropDownListElement;

            string strTheme = _element.Text;

            Theme theme = ThemeResolutionService.GetTheme(strTheme);

            if (theme != null && ApplicationSecurityConstants._activeUser != null)
            {
                ThemeResolutionService.ApplicationThemeName = theme.Name;
            }
        }

        private void mnuCreditPayment_Click(object sender, EventArgs e)
        {
            FrmAcceptCreditPayment _creditPayment = new FrmAcceptCreditPayment();
            _creditPayment.MdiParent = this;
            _creditPayment.Show();
        }

        private void cmdEmployee_Click(object sender, EventArgs e)
        {
            FrmEmployee _employee = new FrmEmployee();
            _employee.MdiParent = this;
            _employee.Show();
        }

        private void cmdPayMethod_Click(object sender, EventArgs e)
        {
            FrmPaymentMethod _paymentMethod = new FrmPaymentMethod();
            _paymentMethod.MdiParent = this;
            _paymentMethod.Show();
        }

        private void cmdSuppliers_Click(object sender, EventArgs e)
        {
            FrmSupplier _supplier = new FrmSupplier();
            _supplier.MdiParent = this;
            _supplier.Show();
        }

        private void cmdInventory_Click(object sender, EventArgs e)
        {
            FrmInventoryMaintenance _inventoryMaintenance = new FrmInventoryMaintenance();
            _inventoryMaintenance.MdiParent = this;
            _inventoryMaintenance.Show();
        }

        private void cmdTransfers_Click(object sender, EventArgs e)
        {
            FrmInterBranchTransfer _interBranchTransfer = new FrmInterBranchTransfer();
            _interBranchTransfer.MdiParent = this;
            _interBranchTransfer.Show();
        }

        private void cmdMaintenance_Click(object sender, EventArgs e)
        {
            FrmInventoryServiceOrder _serviceOrder = new FrmInventoryServiceOrder();
            _serviceOrder.MdiParent = this;
            _serviceOrder.Show();
        }

        private void cmdQuotation_Click(object sender, EventArgs e)
        {
            FrmQuotation _quotation = new FrmQuotation();
            _quotation.MdiParent = this;
            _quotation.Show();
        }

        private void cmdPOS_Click(object sender, EventArgs e)
        {
            FrmSaleTransaction _saleTransaction = new FrmSaleTransaction();
            _saleTransaction.MdiParent = this;
            _saleTransaction.Show();
        }

        private void cmdIssueItems_Click(object sender, EventArgs e)
        {
            FrmItemDelivery _itemDelivery = new FrmItemDelivery();
            _itemDelivery.MdiParent = this;
            _itemDelivery.Show();
        }

        private void cmdUserPrivileges_Click(object sender, EventArgs e)
        {
            SystemUserGroupForm _systemUserGroup = new SystemUserGroupForm();
            _systemUserGroup.MdiParent = this;
            _systemUserGroup.Show();
        }

        private void cmdUser_Click(object sender, EventArgs e)
        {
            SystemUserForm _addSysUser = new SystemUserForm();
            _addSysUser.MdiParent = this;
            _addSysUser.Show();
        }

        private void mnuCustomerReturns_Click(object sender, EventArgs e)
        {
            FrmCustomerReturns _customerReturns = new FrmCustomerReturns();
            _customerReturns.MdiParent = this;
            _customerReturns.Show();
        }

        private void mnuApplyCustPayment_Click(object sender, EventArgs e)
        {
            FrmApplyCustomerPayment _applyCustomerPayment = new FrmApplyCustomerPayment();
            _applyCustomerPayment.MdiParent = this;
            _applyCustomerPayment.Show();
        }

        private void cmdApplyCustPayment_Click(object sender, EventArgs e)
        {
            FrmApplyCustomerPayment _applyCustomerPayment = new FrmApplyCustomerPayment();
            _applyCustomerPayment.MdiParent = this;
            _applyCustomerPayment.Show();
        }

        private void mnuReceiptNotes_Click(object sender, EventArgs e)
        {
            FrmReceiptNotes _receiptNotes = new FrmReceiptNotes();
            _receiptNotes.MdiParent = this;
            _receiptNotes.Show();
        }

        private void mnuSystemVoids_Click(object sender, EventArgs e)
        {
            FrmVoidRequests _voidRequests = new FrmVoidRequests();
            _voidRequests.MdiParent = this;
            _voidRequests.Show();
        }

        private void mnuVoidRequestStatus_Click(object sender, EventArgs e)
        {
            FrmVoidRequestApproval _voidRequestApproval = new FrmVoidRequestApproval();
            _voidRequestApproval.MdiParent = this;
            _voidRequestApproval.Show();
        }

        private void mnuReprints_Click(object sender, EventArgs e)
        {
            FrmReportReprint _reportReprint = new FrmReportReprint();
            _reportReprint.MdiParent = this;
            _reportReprint.Show();
        }

        private void cmdReprints_Click(object sender, EventArgs e)
        {
            FrmReportReprint _reportReprint = new FrmReportReprint();
            _reportReprint.MdiParent = this;
            _reportReprint.Show();
        }

        private void mnuCashierLiability_Click(object sender, EventArgs e)
        {
            FrmCashierLiability _cashierLiability = new FrmCashierLiability();
            _cashierLiability.MdiParent = this;
            _cashierLiability.Show();
        }

        private void mnuInvoiceRegister_Click(object sender, EventArgs e)
        {
            FrmInvoiceRegister _invoiceRegister = new FrmInvoiceRegister();
            _invoiceRegister.MdiParent = this;
            _invoiceRegister.Show();
        }

        private void mnuRentalPayment_Click(object sender, EventArgs e)
        {
            FrmSaleTransactionPayment _saleTransactionPayment = new FrmSaleTransactionPayment();
            _saleTransactionPayment.MdiParent = this;
            _saleTransactionPayment.Show();
        }

        private void mnuCashRefund_Click(object sender, EventArgs e)
        {
            FrmCashRefund _cashRefund = new FrmCashRefund();
            _cashRefund.MdiParent = this;
            _cashRefund.Show();
        }

        private void mnuDepositRefund_Click(object sender, EventArgs e)
        {
            FrmRefundSecurityDeposit _refundSecurityDeposit = new FrmRefundSecurityDeposit();
            _refundSecurityDeposit.MdiParent = this;
            _refundSecurityDeposit.Show();
        }

        private void cmdVoids_Click(object sender, EventArgs e)
        {
            FrmSaleTransactionPayment _saleTransactionPayment = new FrmSaleTransactionPayment();
            _saleTransactionPayment.MdiParent = this;
            _saleTransactionPayment.Show();
        }

        private void mnuOrder_Click(object sender, EventArgs e)
        {
            FrmOrder _order = new FrmOrder();
            _order.MdiParent = this;
            _order.Show();
        }

        private void cmdOrder_Click(object sender, EventArgs e)
        {
            FrmOrder _order = new FrmOrder();
            _order.MdiParent = this;
            _order.Show();
        }

        private void mnuRevenueOverview_Click(object sender, EventArgs e)
        {
            FrmRevenueOverview _revenueOverview = new FrmRevenueOverview();
            _revenueOverview.MdiParent = this;
            _revenueOverview.Show();
        }

        private void mnuBins_Click(object sender, EventArgs e)
        {
            FrmInventoryBin _inventoryBin = new FrmInventoryBin();
            _inventoryBin.MdiParent = this;
            _inventoryBin.Show();
        }

        private void mnuClosedSale_Click(object sender, EventArgs e)
        {
            FrmClosedSaleTransaction _closedSaleTransaction = new FrmClosedSaleTransaction();
            _closedSaleTransaction.MdiParent = this;
            _closedSaleTransaction.Show();
        }

        private void mnuSaleBundle_Click(object sender, EventArgs e)
        {
            FrmSaleBundle _saleBundle = new FrmSaleBundle();
            _saleBundle.MdiParent = this;
            _saleBundle.Show();
        }

        private void mnuEnquiry_Click(object sender, EventArgs e)
        {
            FrmEnquiry _enquiry = new FrmEnquiry();
            _enquiry.MdiParent = this;
            _enquiry.Show();
        }

        private void mnuEnquiryUpdate_Click(object sender, EventArgs e)
        {
            FrmEnquiryUpdate _enquiryUpdate = new FrmEnquiryUpdate();
            _enquiryUpdate.MdiParent = this;
            _enquiryUpdate.Show();
        }

        private void mnuEnPriority_Click(object sender, EventArgs e)
        {
            FrmEnquiryPriority _enquiryPriority = new FrmEnquiryPriority();
            _enquiryPriority.MdiParent = this;
            _enquiryPriority.Show();
        }

        private void mnuWorkOrder_Click(object sender, EventArgs e)
        {
            FrmWorkOrder _workOrder = new FrmWorkOrder();
            _workOrder.MdiParent = this;
            _workOrder.Show();
        }

        private void mnuServiceCharge_Click(object sender, EventArgs e)
        {
            FrmService _service = new FrmService();
            _service.MdiParent = this;
            _service.Show();
        }

        private void mnuAccessories_Click(object sender, EventArgs e)
        {
            FrmInventoryAccessories _inventoryAccessories = new FrmInventoryAccessories();
            _inventoryAccessories.MdiParent = this;
            _inventoryAccessories.Show();
        }

        private void mnuFuelType_Click(object sender, EventArgs e)
        {
            FrmFuelType _fuelType = new FrmFuelType();
            _fuelType.MdiParent = this;
            _fuelType.Show();
        }

        private void mnuTyreSize_Click(object sender, EventArgs e)
        {
            FrmTyreSize _tyreSize = new FrmTyreSize();
            _tyreSize.MdiParent = this;
            _tyreSize.Show();
        }

        private void mnuRefunds_Click(object sender, EventArgs e)
        {

        }

        private void mnuRptInventoryList_Click(object sender, EventArgs e)
        {
            FrmInventoryReport _inventoryReport = new FrmInventoryReport();
            _inventoryReport.MdiParent = this;
            _inventoryReport.Show();
        }

        private void radMenuItem4_Click(object sender, EventArgs e)
        {

        }

        private void mnuRptQuotation_Click(object sender, EventArgs e)
        {
            FrmQuotationRegister quotationRegister = new FrmQuotationRegister();
            quotationRegister.MdiParent = this;
            quotationRegister.Show();
        }

        private void mnuRptRentalDetails_Click(object sender, EventArgs e)
        {
            FrmRentalDetails rentalDetails = new FrmRentalDetails();
            rentalDetails.MdiParent = this;
            rentalDetails.Show();
        }
    }
}