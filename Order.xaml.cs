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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using Resto.Front.Api.Extensions;

namespace Resto.Front.Api.TestPlugin
{
    public sealed partial class Order : UserControl
    {
        Random random = new Random();
        int gCount = 1;
        //int tCount = 1;
        int oCount = 1;
        bool gC = false;
        //bool tC = false;
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
            var paymentsType = PluginContext.Operations.GetPaymentTypes();
            foreach (var paymentType in paymentsType)
            {
                PaymentComboBox.Items.Add(paymentType.Name);
            }
            SearchBox.ItemsSource = Products.Select(p => $"{p.Name} | {p.Article}").ToList();
            GetAll();
            btn_rndGuests.IsEnabled = false;
            //btn_rndNmbTbl.IsEnabled = false;
            btn_rndOrd.IsEnabled = false;
        }
        private void FilterProductsByOrderList()
        {
            var selectedArticles = OrderList.Items.Cast<string>().Select(item =>
                {
                    var parts = item.Split('|');
                    return parts.Length == 2 ? parts[1].Trim() : null;
                }).Where(article => article != null).ToHashSet();
            ProductForOrder = ProductForOrder.Where(product => selectedArticles.Contains(product.Number)).ToList();
        }
        private void GetAll()
        {            
            var menu = PluginContext.Operations.GetHierarchicalMenu();
            var rootProducts = menu.Products;
            var nestedProducts = GetAllProductsRecursively();
            Products = GetSimpleProductList();
            ProductForOrder = nestedProducts;
            var allProducts = rootProducts.Concat(nestedProducts).Distinct().ToList();
            SearchBox.ItemsSource = Products.Select(p => $"{p.Name} | {p.Article}").ToList();
        }
        private List<Product> GetSimpleProductList()
        {
            var menu = PluginContext.Operations.GetHierarchicalMenu();
            var products = GetAllProductsRecursively();
            return products.OrderBy(p => p.Name).Select(p => new Product
            {
                Article = p.Number,
                Name = p.Name
            }).ToList();
        }
        public static List<IProduct> GetAllProductsRecursively()
        {
            var result = new List<IProduct>();
            var products = PluginContext.Operations.GetAllProducts()
                .Where(product => product.Price > 0 && product.Type == ProductType.Dish && product.Scale == null && product.IsActive) ;            
            result.AddRange(products);
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
                // Фильтрация продуктов
                FilterProductsByOrderList();
                // Получаем столы из секций ресторана
                var tables = GetAvailableTables();
                // Создание сессии редактирования и заказа
                var editSession = PluginContext.Operations.CreateEditSession();
                //var selectedTables = GetSelectedTables(tables); 
                var newOrder = editSession.CreateOrder(null);
                // Установка источника заказа
                editSession.ChangeOrderOriginName("Test Plugin", newOrder);
                // Добавление гостей
                IOrderGuestItemStub lastGuest = AddGuestsToOrder(editSession, newOrder);
                // Добавление товаров
                AddProductsToOrder(editSession, newOrder, lastGuest);
                // Определение типа оплаты
                var paymentType = GetSelectedPaymentType();
                if (paymentType == null) return;
                // Применение изменений
                PluginContext.Operations.SubmitChanges(editSession);
                // Оплата заказа
                PayOrder(paymentType);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Общая ошибка создания заказа. Проверьте поля\r\n" + ex.Message);
            }
        }
        private List<ITable> GetAvailableTables()
        {
            return PluginContext.Operations.GetRestaurantSections().Where(s => s.Tables?.Any() == true).SelectMany(s => s.Tables).OrderBy(t => t.Number).ToList();
        }

        //private List<ITable> GetSelectedTables(List<ITable> tables)
        //{
        //    int tableNumber = tC ? tCount : Convert.ToInt32(TableNumberBox.Text);
        //    return tables.Where(t => t.Number == tableNumber).ToList();
        //}
        private IOrderGuestItemStub AddGuestsToOrder(IEditSession editSession, INewOrderStub order)
        {
            int guestCount;
            if (gC)
            {
                guestCount = gCount;
            }
            else if (!int.TryParse(GuestCountBox.Text, out guestCount))
            {
                MessageBox.Show("Некорректное число гостей");
                return null;
            }
            IOrderGuestItemStub lastGuest = editSession.AddOrderGuest("Гость номер: 1", order);

            for (int i = 2; i <= guestCount; i++)
            {
                lastGuest = editSession.AddOrderGuest($"Гость номер: {i}", order);
            }
            return lastGuest;
        }
        private void AddProductsToOrder(IEditSession editSession, INewOrderStub order, IOrderGuestItemStub lastGuest)
        {
            foreach (var product in ProductForOrder)
            {
                editSession.AddOrderProductItem(random.Next(1, 10), product, order, lastGuest, null);
            }
        }
        private IPaymentType GetSelectedPaymentType()
        {
            if (PaymentComboBox.SelectedItem == null)
            {
                MessageBox.Show("Не выбран тип оплаты");
                return null;
            }
            string selectedText = PaymentComboBox.SelectedItem.ToString();
            try
            {
                PaymentTypeKind paymentTypeKind;
                switch (selectedText)
                {
                    case "Наличные":
                        paymentTypeKind = PaymentTypeKind.Cash;
                        return PluginContext.Operations.GetPaymentTypes().FirstOrDefault(x => x.Kind == paymentTypeKind);

                    case "Банковские карты":
                        paymentTypeKind = PaymentTypeKind.Card;
                        return PluginContext.Operations.GetPaymentTypesToPayOutOnUser().FirstOrDefault(x => x.Kind == paymentTypeKind);

                    default:
                        MessageBox.Show("Неизвестный способ оплаты: " + selectedText);
                        return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при определении типа оплаты:\r\n" + ex.Message);
                return null;
            }
        }
        private void PayOrder(IPaymentType paymentType)
        {
            try
            {
                var order = PluginContext.Operations.GetOrders().Last(o => o.Status == OrderStatus.New);
                string selectedText = PaymentComboBox.SelectedItem.ToString();
                if (selectedText == "Наличные")
                {
                    PluginContext.Operations.AddPaymentItem(order.FullSum, null, paymentType, order, PluginContext.Operations.GetDefaultCredentials());
                    PluginContext.Operations.PayOrder(order, true, PluginContext.Operations.GetDefaultCredentials());
                }
                else if (selectedText == "Банковские карты")
                {
                    PluginContext.Operations.PayOrderAndPayOutOnUser(order, true, paymentType, order.ResultSum, PluginContext.Operations.GetDefaultCredentials());
                }
            }
            catch (PaymentActionFailedException ex)
            {
                PluginContext.Log.Error($"Во время оплаты произошла ошибка: {ex.Details}");
            }
            catch (Exception ex)
            {
                PluginContext.Log.Error(ex.ToString());
            }
        }
        //private void btn_rndNmbTbl_Click(object sender, RoutedEventArgs e)
        //{
        //    tCount = random.Next(1, 30);
        //    TableNumberBox.Text = tCount.ToString();                       
        //}
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
                btn_crt.IsEnabled = false;
                if (oC)
                {
                    for (int i = 0; i < oCount; i++)
                    {
                        CreateOrder();
                        Thread.Sleep(333);
                    }
                }
                else
                {
                    for (int i = 0; i < Convert.ToInt32(OrderCountBox.Text); i++)
                    {
                        CreateOrder();
                        Thread.Sleep(333);
                    }
                }
                btn_crt.IsEnabled = true;
                ProductForOrder.Clear();
                OrderList.Items.Clear();
                GetAll();
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
        //private void LimitTo30(object sender, TextChangedEventArgs e)
        //{
        //    var tb = sender as TextBox;
        //    if (int.TryParse(tb.Text, out int value))
        //    {
        //        if (value > 30)
        //        {
        //            tb.Text = "30";
        //            tb.CaretIndex = tb.Text.Length;
        //        }
        //    }
        //}
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
        //private void chk_AutoTbl_Checked(object sender, RoutedEventArgs e)
        //{
        //    TableNumberBox.IsReadOnly = true;
        //    TableNumberBox.Background = Brushes.LightGray;
        //    tC = true;
        //    btn_rndNmbTbl.IsEnabled = true;
        //}
        //private void chk_AutoTbl_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    TableNumberBox.IsReadOnly = false;
        //    TableNumberBox.Background = Brushes.White;
        //    tC = false;
        //    btn_rndNmbTbl.IsEnabled = false;
        //}
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
// commit