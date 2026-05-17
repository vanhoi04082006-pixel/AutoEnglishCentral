using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace AutoWatchMultipleVideos
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("=== TOOL AUTO WATCH VIDEOS (1 CHROME - NHIỀU TAB) ===");
            Console.WriteLine("Nhập các link video (ngăn cách nhau bởi dấu phẩy, khoảng trắng hoặc xuống dòng):");

            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Không có link nào được nhập. Đang thoát tool...");
                return;
            }

            string[] urls = input.Split(new char[] { ',', ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> videoLinks = new List<string>(urls);

            Console.WriteLine($"\nĐã nhận {videoLinks.Count} link.");
            Console.Write("Bạn muốn chạy bao nhiêu Tab cùng lúc? (Khuyên dùng: 3-5): ");
            int maxTabs = 3;
            if (int.TryParse(Console.ReadLine(), out int inputThreads) && inputThreads > 0)
            {
                maxTabs = inputThreads;
            }

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--mute-audio");
            options.AddArgument("--autoplay-policy=no-user-gesture-required");

            using (IWebDriver driver = new ChromeDriver(options))
            {
                // ==========================================
                // BƯỚC 1: ĐĂNG NHẬP 1 LẦN DUY NHẤT
                // ==========================================
                driver.Navigate().GoToUrl("https://vi.englishcentral.com/authentication/login");
                Console.WriteLine("\n[HỆ THỐNG] Đã mở trình duyệt!");
                Console.WriteLine(">>> HÃY THAO TÁC ĐĂNG NHẬP TRÊN TRÌNH DUYỆT <<<");
                Console.WriteLine("Sau khi đăng nhập thành công, hãy quay lại đây và NHẤN ENTER để tool bắt đầu cày...");
                Console.ReadLine(); // Chờ user bấm Enter

                IJavaScriptExecutor js = (IJavaScriptExecutor)driver;

                // activeTabs: Lưu trữ [ID của Tab] và [Link đang chạy trên Tab đó]
                Dictionary<string, string> activeTabs = new Dictionary<string, string>();
                Dictionary<string, int> tabErrorCount = new Dictionary<string, int>();
                int currentLinkIndex = 0;

                Console.WriteLine($"\nĐang bắt đầu cày {videoLinks.Count} link trên {maxTabs} Tab...\n");

                // ==========================================
                // BƯỚC 2: HÀM HỖ TRỢ MỞ TAB MỚI
                // ==========================================
                void OpenNextLink()
                {
                    if (currentLinkIndex < videoLinks.Count)
                    {
                        string url = videoLinks[currentLinkIndex];
                        // Ra lệnh cho Chrome mở 1 Tab trắng mới tinh
                        driver.SwitchTo().NewWindow(WindowType.Tab);

                        // Truy cập link vào Tab mới đó
                        driver.Navigate().GoToUrl(url);

                        // Lưu ID của tab này vào danh sách theo dõi
                        activeTabs.Add(driver.CurrentWindowHandle, url);
                        tabErrorCount.Add(driver.CurrentWindowHandle, 0);

                        string videoId = url.Substring(url.LastIndexOf('/') + 1);
                        Console.WriteLine($"[Tab Mới] Video {videoId} đang mở...");
                        currentLinkIndex++;
                    }
                }

                // Lưu lại ID của tab đăng nhập ban đầu
                string loginTab = driver.CurrentWindowHandle;

                // Mở hàng loạt các Tab theo số lượng maxTabs bạn yêu cầu
                for (int i = 0; i < maxTabs && currentLinkIndex < videoLinks.Count; i++)
                {
                    OpenNextLink();
                }

                // Đóng cái Tab login ban đầu đi cho giao diện Chrome gọn gàng
                if (activeTabs.Count > 0)
                {
                    driver.SwitchTo().Window(loginTab);
                    driver.Close();
                }

                // ==========================================
                // BƯỚC 3: VÒNG LẶP CHĂM SÓC CÁC TAB
                // ==========================================
                while (activeTabs.Count > 0)
                {
                    Thread.Sleep(2000); // Tốc độ quét qua các tab

                    // Lấy danh sách ID các tab đang hoạt động
                    List<string> handles = new List<string>(activeTabs.Keys);

                    foreach (string handle in handles)
                    {
                        try
                        {
                            // Nhảy (Switch) sang tab hiện tại để bơm JavaScript vào
                            driver.SwitchTo().Window(handle);

                            string currentUrl = activeTabs[handle];
                            string videoId = currentUrl.Substring(currentUrl.LastIndexOf('/') + 1);

                            string checkScript = @"
    var videos = document.querySelectorAll('video');
    if (videos.length === 0) return 'NO_VIDEO';
    
    var v = videos[0];

    // Bơm tốc độ phát video lên x5 lần (Bạn có thể đổi số 5.0 thành 10.0 nếu muốn)
    v.playbackRate = 5.0;

    if (v.paused && v.currentTime < 0.5) {
        v.play();
        return 'TRYING_TO_PLAY';
    }
    
    if (v.ended || (v.duration > 0 && v.currentTime >= v.duration - 1)) {
        return 'ENDED';
    }
    
    if (v.paused && v.currentTime >= 0.5) {
        var skipBtn = document.querySelector('.fa-step-forward');
        if (skipBtn) return 'SKIP_TO_NEXT'; 
    }
    
    return 'PLAYING';
";

                            string status = js.ExecuteScript(checkScript)?.ToString();

                            if (status == "NO_VIDEO")
                            {
                                tabErrorCount[handle]++;
                                if (tabErrorCount[handle] > 5)
                                {
                                    Console.WriteLine($"[Bỏ qua] Video {videoId} bị lỗi không tải được thẻ video.");
                                    status = "CLOSE_TAB";
                                }
                            }
                            else if (status == "SKIP_TO_NEXT" || status == "ENDED")
                            {
                                Console.WriteLine($"[Hoàn thành] Video {videoId} đã xem xong!");
                                status = "CLOSE_TAB";
                            }

                            // Nếu tab báo xong (hoặc lỗi), ta sẽ đóng nó và mở bài tiếp theo
                            if (status == "CLOSE_TAB")
                            {
                                // Kéo link mới mở thêm 1 tab nữa thay thế
                                OpenNextLink();

                                // Quay lại đúng cái tab vừa xem xong để tắt nó đi
                                driver.SwitchTo().Window(handle);
                                driver.Close();

                                // Xóa tab này khỏi bộ nhớ theo dõi của C#
                                activeTabs.Remove(handle);
                                tabErrorCount.Remove(handle);
                            }
                        }
                        catch (Exception)
                        {
                            // Nếu tab đang bận load hoặc bị gián đoạn, bỏ qua lượt check này
                        }
                    }
                }

                Console.WriteLine("\n=== HOÀN THÀNH: ĐÃ DUYỆT QUA TẤT CẢ CÁC LINK! ===");
                Console.WriteLine("Nhấn phím bất kỳ để đóng trình duyệt...");
                Console.ReadKey();

                try { driver.Quit(); } catch { } // Dọn dẹp an toàn
            }
        }
    }
}