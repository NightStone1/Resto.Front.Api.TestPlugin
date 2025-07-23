using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Resto.Front.Api;
using Resto.Front.Api.Data;
using Resto.Front.Api.Editors;
using Resto.Front.Api.Data.Organization.Sections;
using System.Windows.Controls.Primitives;
using System.Security.AccessControl;
using System.Windows.Controls;
using Resto.Front.Api.Data.Assortment;
using Resto.Front.Api.Attributes.JetBrains;
using Resto.Front.Api.TestPlugin.Restaurant;
using Resto.Front.Api.Data.PreliminaryOrders;
using Resto.Front.Api.Exceptions;
using Resto.Front.Api.TestPlugin.PreliminaryOrders;
using static Resto.Front.Api.TestPlugin.Order;
using System.Windows.Input;
using System.Windows.Media;
using Resto.Front.Api.Editors.Stubs;
using Resto.Front.Api.Data.Orders;
using Resto.Front.Api.Data.Payments;
using System.Threading;

namespace Resto.Front.Api.TestPlugin
{
    public sealed partial class Order : UserControl
    {
        Random random = new Random();
        int gCount = 1;
        int tCount = 1;
        int oCount = 1;
        bool gC = false;
        bool tC = false;
        bool oC = false;
        public class Product
        {
            public string Article { get; set; }
            public string Name { get; set; }
        }
        private List<Product> Products = new List<Product>
        {
        };
        private List<IProduct> ProductForOrder = new List<IProduct>
        {
        };
       
        public Order()
        {
            InitializeComponent();
            SearchBox.Loaded += (s, e) =>
            {
                var textBox = (TextBox)SearchBox.Template.FindName("PART_EditableTextBox", SearchBox);
                if (textBox != null)
                {
                    textBox.TextChanged += SearchBox_TextChanged;
                }
            };
            SearchBox.ItemsSource = Products.Select(p => $"{p.Name} | {p.Article}").ToList();
            GetAll();
            btn_rndGuests.IsEnabled = false;
            btn_rndNmbTbl.IsEnabled = false;
            btn_rndOrd.IsEnabled = false;
        }
        private void FilterProductsByOrderList()
        {
            // Извлекаем артикулы из OrderList (в строке формат "Имя | Артикул")
            var selectedArticles = OrderList.Items.Cast<string>().Select(item =>
                {
                    var parts = item.Split('|');
                    return parts.Length == 2 ? parts[1].Trim() : null;
                }).Where(article => article != null).ToHashSet();
            ProductForOrder = ProductForOrder.Where(product => selectedArticles.Contains(product.Number)).ToList();
        }
        private void GetAll()
        {            
            var paymentsType = PluginContext.Operations.GetPaymentTypes();
            foreach (var paymentType in paymentsType)
            {
                PaymentComboBox.Items.Add(paymentType.Name);
            }
            var menu = PluginContext.Operations.GetHierarchicalMenu();
            var rootProducts = menu.Products;
            var nestedProducts = GetAllProductsRecursively(menu.ProductGroups);
            Products = GetSimpleProductList();
            ProductForOrder = nestedProducts;
            var allProducts = rootProducts.Concat(nestedProducts).Distinct().ToList();
            SearchBox.ItemsSource = Products.Select(p => $"{p.Name} | {p.Article}").ToList();
        }
        private List<Product> GetSimpleProductList()
        {
            var menu = PluginContext.Operations.GetHierarchicalMenu();
            var products = GetAllProductsRecursively(menu.ProductGroups);
            return products.OrderBy(p => p.Name).Select(p => new Product
            {
                Article = p.Number,
                Name = p.Name
            }).ToList();
        }
        public static List<IProduct> GetAllProductsRecursively(IEnumerable<IProductGroup> rootGroups)
        {
            var result = new List<IProduct>();
            var stack = new Stack<IProductGroup>(rootGroups);
            while (stack.Count > 0)
            {
                var group = stack.Pop();
                var products = PluginContext.Operations.GetChildProductsByProductGroup(group).Where(product => product.Price != 0 && product.Type == ProductType.Dish && product.Scale == null) ;
                result.AddRange(products);
                var childGroups = PluginContext.Operations.GetChildGroupsByProductGroup(group);
                foreach (var child in childGroups)
                    stack.Push(child);
            }
            return result;
        }
        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = sender as ComboBox;
            var textBox = (TextBox)combo.Template.FindName("PART_EditableTextBox", combo);
            if (textBox != null)
            {
                textBox.TextChanged += SearchBox_TextChanged;
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var input = SearchBox.Text.ToLower();

            List<string> results;

            if (string.IsNullOrWhiteSpace(input))
            {
                results = Products.Select(p => $"{p.Name} | {p.Article}").ToList();
            }
            else
            {
                results = Products
                    .Where(p => p.Name.ToLower().Contains(input) || p.Article.Contains(input)).Select(p => $"{p.Name} | {p.Article}").ToList();
            }
            SearchBox.ItemsSource = results;
            SearchBox.IsDropDownOpen = results.Any();
        }
        private void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchBox.SelectedItem != null)
            {
                var selectedItem = SearchBox.SelectedItem.ToString();
                if (!OrderList.Items.Contains(selectedItem))
                {
                    OrderList.Items.Add(selectedItem);
                }
                SearchBox.Text = "";
                SearchBox.ItemsSource = null;
                SearchBox.IsDropDownOpen = false;
                SearchBox.SelectedItem = null;
            }
        }
        private void CreateOrder()
        {
            try
            {
                FilterProductsByOrderList();
                var sections = PluginContext.Operations.GetRestaurantSections().Where(s => s.Tables != null && s.Tables.Count != 0);
                List<ITable> tables = new List<ITable>();
                foreach (var section in sections)
                {
                    foreach (var table in section.Tables.OrderBy(t => t.Number))
                    {
                        tables.Add(table);
                    }
                }
                var credentials1 = PluginContext.Operations.GetDefaultCredentials();
                var editSession = PluginContext.Operations.CreateEditSession();
                INewOrderStub newOrder;
                if (tC)
                {
                    newOrder = editSession.CreateOrder(tables.Where(s => s.Number == tCount).ToList());
                }
                else
                {
                    newOrder = editSession.CreateOrder(tables.Where(s => s.Number == Convert.ToInt32(TableNumberBox.Text)).ToList());
                }
                editSession.ChangeOrderOriginName("Test Plugin", newOrder);
                var guest = editSession.AddOrderGuest("Гость номер: 1", newOrder);
                if (gC)
                {
                    for (int i = 1; i < gCount - 1; i++)
                    {
                        guest = editSession.AddOrderGuest($"Гость номер: {i}", newOrder);
                    }
                }
                else
                {
                    for (int i = 1; i < Convert.ToInt32(GuestCountBox.Text) - 1; i++)
                    {
                        guest = editSession.AddOrderGuest($"Гость номер: {i}", newOrder);
                    }
                }
                foreach (var product in ProductForOrder)
                {
                    editSession.AddOrderProductItem(random.Next(1, 10), product, newOrder, guest, null);
                }
                PaymentTypeKind paymentTypeKind = new PaymentTypeKind();
                try
                {
                    if (PaymentComboBox.SelectedItem.ToString() == "Наличные")
                    {
                        paymentTypeKind = PaymentTypeKind.Cash;
                    }
                    else if (PaymentComboBox.SelectedItem.ToString() == "Банковские карты")
                    {
                        paymentTypeKind = PaymentTypeKind.Card;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка: Не выбран тип оплаты\r" + ex.Message);
                }
                var result = PluginContext.Operations.SubmitChanges(editSession);
                //
                //Закомментировано по причине исключения 
                //
                //try
                //{
                //    var order = PluginContext.Operations.GetOrders().Last(o => o.Status == OrderStatus.New || o.Status == OrderStatus.Bill);
                //    var paymentType = PluginContext.Operations.GetPaymentTypesToPayOutOnUser().First(x => x.Kind == paymentTypeKind);
                //    var credentials2 = PluginContext.Operations.GetDefaultCredentials();
                //    PluginContext.Operations.PayOrderAndPayOutOnUser(order, true, paymentType, order.ResultSum, credentials2);
                //}
                //catch (Exception ex)
                //{
                //    MessageBox.Show("Скорее всего нет лицензии\r" + ex.Message);
                //}                
            }
            catch (Exception ex)
            {
                MessageBox.Show("Общая ошибка создания заказа. Проверьте поля\r" + ex.Message);
            }
        }
        private void btn_rndNmbTbl_Click(object sender, RoutedEventArgs e)
        {
            tCount = random.Next(1, 30);
            TableNumberBox.Text = tCount.ToString();                       
        }
        private void btn_rndGuests_Click(object sender, RoutedEventArgs e)
        {
            gCount = random.Next(1, 999);
            GuestCountBox.Text = gCount.ToString();
        }
        private void btn_rndOrd_Click(object sender, RoutedEventArgs e)
        {
            oCount = random.Next(1, 10);
            OrderCountBox.Text = oCount.ToString();
        }
        private void btn_crt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (oC)
                {
                    for (int i = 0; i < oCount; i++)
                    {
                        CreateOrder();
                        Thread.Sleep(333); // Задержка 1 секунда (1000 мс)
                    }
                }
                else
                {
                    for (int i = 0; i < Convert.ToInt32(OrderCountBox.Text); i++)
                    {
                        CreateOrder();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Общая ошибка создания заказа. Проверьте поля\r" + ex.Message);
            }
            
        }
        private void NumericOnly(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }
        private void LimitTo999(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (int.TryParse(tb.Text, out int value))
            {
                if (value > 999)
                {
                    tb.Text = "999";
                    tb.CaretIndex = tb.Text.Length;
                }
            }
        }
        private void LimitTo30(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (int.TryParse(tb.Text, out int value))
            {
                if (value > 30)
                {
                    tb.Text = "30";
                    tb.CaretIndex = tb.Text.Length;
                }
            }
        }
        private void LimitTo10(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (int.TryParse(tb.Text, out int value))
            {
                if (value > 10)
                {
                    tb.Text = "10";
                    tb.CaretIndex = tb.Text.Length;
                }
            }
        }
        private void chk_AutoOrd_Checked(object sender, RoutedEventArgs e)
        {
            OrderCountBox.IsReadOnly = true;
            OrderCountBox.Background = Brushes.LightGray;
            oC = true;
            btn_rndOrd.IsEnabled = true;
        }
        private void chk_AutoOrd_Unchecked(object sender, RoutedEventArgs e)
        {
            OrderCountBox.IsReadOnly = false;
            OrderCountBox.Background = Brushes.White;
            oC = false;
            btn_rndOrd.IsEnabled = false;
        }
        private void chk_AutoTbl_Checked(object sender, RoutedEventArgs e)
        {
            TableNumberBox.IsReadOnly = true;
            TableNumberBox.Background = Brushes.LightGray;
            tC = true;
            btn_rndNmbTbl.IsEnabled = true;
        }
        private void chk_AutoTbl_Unchecked(object sender, RoutedEventArgs e)
        {
            TableNumberBox.IsReadOnly = false;
            TableNumberBox.Background = Brushes.White;
            tC = false;
            btn_rndNmbTbl.IsEnabled = false;
        }
        private void chk_AutoGuests_Checked(object sender, RoutedEventArgs e)
        {
            GuestCountBox.IsReadOnly = true;
            GuestCountBox.Background = Brushes.LightGray;
            gC = true;
            btn_rndGuests.IsEnabled = true;
        }
        private void chk_AutoGuests_Unchecked(object sender, RoutedEventArgs e)
        {
            GuestCountBox.IsReadOnly = false;
            GuestCountBox.Background = Brushes.White;
            gC = false;
            btn_rndGuests.IsEnabled = false;
        }
    }
}
