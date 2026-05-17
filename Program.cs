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
            
            Console.WriteLine("=== TOOL AUTO ENGLISH CENTRAL (FULL TÍNH NĂNG TỐI THƯỢNG) ===");
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

            // ==========================================
            // CẤU HÌNH CHROME BẢN QUYỀN
            // ==========================================
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--mute-audio");
            options.AddArgument("--autoplay-policy=no-user-gesture-required");

            // Tự động cấp quyền Allow cho Micro, Camera và Thông báo (Không bao giờ bị popup hỏi lại)
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_mic", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.media_stream_camera", 1);
            options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 1);

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
                        driver.SwitchTo().NewWindow(WindowType.Tab); 
                        driver.Navigate().GoToUrl(url);
                        
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

                    List<string> handles = new List<string>(activeTabs.Keys);

                    foreach (string handle in handles)
                    {
                        try
                        {
                            driver.SwitchTo().Window(handle); 
                            
                            string currentUrl = activeTabs[handle];
                            string videoId = currentUrl.Substring(currentUrl.LastIndexOf('/') + 1);

                            // ==========================================
                            // ĐOẠN JAVASCRIPT LÕI TÍCH HỢP TOÀN BỘ TÍNH NĂNG
                            // ==========================================
                            string checkScript = @"
                                var videos = document.querySelectorAll('video');
                                if (videos.length === 0) return 'NO_VIDEO';
                                
                                var v = videos[0];
                                v.playbackRate = 5.0; // Ép tốc độ x5

                                // Đặt lại bộ nhớ khi video chạy tiếp sang câu mới
                                if (!v.paused) {
                                    window.guessIndex = 0;
                                    window.micClicked = false; // Reset trí nhớ về trạng thái ban đầu
                                }

                                // --- BẢO VỆ TOOL TRONG QUÁ TRÌNH WEB ĐANG THU ÂM ---
                                var micBtn = document.querySelector('#mic-is-ready');
                                if (window.micClicked) {
                                    if (!micBtn || micBtn.offsetParent === null) {
                                        // Nếu đã bấm Mic mà cái nút Mic biến mất -> Nghĩa là Web đang mải thu âm
                                        return 'RECORDING';
                                    }
                                }

                                // --- ƯU TIÊN 1: NÚT 'TIẾP TỤC' CHUYỂN PHẦN HỌC ---
                                var continueBtn = document.querySelector('.next-button');
                                if (continueBtn && continueBtn.offsetParent !== null) {
                                    continueBtn.click();
                                    try { continueBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                    return 'CLICKED_CONTINUE';
                                }

                                // --- ƯU TIÊN 1.5: XỬ LÝ MÀN HÌNH CHUYỂN TIẾP SANG PHẦN NÓI (BẤM 'NÓI X DÒNG') ---
                                var speakFooterBtn = document.querySelector('.speak-mode-footer .footer-button');
                                if (speakFooterBtn && speakFooterBtn.offsetParent !== null) {
                                    speakFooterBtn.click();
                                    try { speakFooterBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                    return 'CLICKED_START_SPEAK_PHASE';
                                }

                                // --- ƯU TIÊN 1.6: BẤM CHỌN MENU 'NÓI' KHI HỌC TỪ XONG RỒI BỊ ĐỨNG YÊN ---
                                var speakMenuBtn = document.querySelector('.activity-list-speak');
                                var learnBtnCompleted = document.querySelector('.activity-list-learn.completed');
                                var speakBtnCompleted = document.querySelector('.activity-list-speak.completed');
                                
                                // Nếu nút Menu 'Nói' hiện, và phần Học từ đã xanh (completed), và phần Nói chưa completed
                                if (speakMenuBtn && speakMenuBtn.offsetParent !== null && learnBtnCompleted && !speakBtnCompleted) {
                                    if ((!speakFooterBtn || speakFooterBtn.offsetParent === null) && (!micBtn || micBtn.offsetParent === null)) {
                                        speakMenuBtn.click();
                                        var innerDiv = speakMenuBtn.querySelector('.speak');
                                        if (innerDiv) innerDiv.click();
                                        try { speakMenuBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                        return 'CLICKED_SPEAK_MENU';
                                    }
                                }

                                // --- ƯU TIÊN 2: BẤM MICRO VÀ CHỜ WEB TỰ CHẤM ĐIỂM ---
                                if (micBtn && micBtn.offsetParent !== null) {
                                    if (!window.micClicked) {
                                        // Bước 2.1: Mới tới câu này -> Bấm Mic
                                        window.micClicked = true;
                                        window.scoreWaitTime = 0;
                                        micBtn.click();
                                        var innerMicIcon = micBtn.querySelector('i');
                                        if (innerMicIcon) innerMicIcon.click();
                                        try { micBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                        return 'STARTED_MIC';
                                    } else {
                                        // Bước 2.2: Nút Mic đã hiện lại sau quá trình ghi âm -> Chờ bảng điểm xuất hiện
                                        window.scoreWaitTime = (window.scoreWaitTime || 0) + 1;
                                        
                                        var hasScore = false;
                                        var scoreWrapper = document.querySelector('.score-bucket-wrapper');
                                        if (scoreWrapper && !scoreWrapper.classList.contains('invisible')) hasScore = true;
                                        
                                        // Kiểm tra dự phòng bằng chữ (Đề phòng class web thay đổi)
                                        if (!hasScore) {
                                            var texts = document.querySelectorAll('div, span');
                                            for(var i=0; i<texts.length; i++) {
                                                if(texts[i].innerText && texts[i].innerText.includes('Điểm phát âm') && texts[i].offsetParent !== null) {
                                                    hasScore = true; break;
                                                }
                                            }
                                        }

                                        // Nếu bảng điểm đã hiện HOẶC đã đợi quá lâu (chống kẹt)
                                        if (hasScore || window.scoreWaitTime >= 3) {
                                            // Click nền video để tắt bảng điểm
                                            var playLayer = document.querySelector('.play-pause-layer');
                                            if (playLayer) {
                                                try { playLayer.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e){}
                                            }

                                            // Bấm nút Next để bỏ qua dòng
                                            var skipLineBtn = document.querySelector('.mic-skip-line-button');
                                            if (skipLineBtn && skipLineBtn.offsetParent !== null) {
                                                skipLineBtn.click();
                                                var innerSkipIcon = skipLineBtn.querySelector('i');
                                                if (innerSkipIcon) innerSkipIcon.click();
                                                try { skipLineBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e){}
                                                window.micClicked = false; // Xóa trí nhớ để qua câu sau
                                                return 'CLICKED_SKIP_LINE';
                                            }
                                        }
                                        return 'WAITING_FOR_SCORE';
                                    }
                                }

                                // --- ƯU TIÊN 3: XỬ LÝ FORM CÀI ĐẶT ---
                                var settingsMenu = document.querySelector('.settings');
                                if (settingsMenu && settingsMenu.offsetParent !== null) { 
                                    var mcBtn = document.querySelector('.multiple-choice-btn');
                                    if (mcBtn && !mcBtn.classList.contains('selected')) {
                                        mcBtn.click();
                                        return 'CLICKED_MULTIPLE_CHOICE';
                                    } else {
                                        var closeX = document.querySelector('.settings-header-close-button');
                                        var closeAlt = document.querySelector('.settings-wrapper-close-btn');
                                        if (closeX && closeX.offsetParent !== null) { closeX.click(); return 'CLOSED_SETTINGS'; } 
                                        else if (closeAlt && closeAlt.offsetParent !== null) { closeAlt.click(); return 'CLOSED_SETTINGS'; }
                                    }
                                }

                                // --- ƯU TIÊN 4: NÚT 'THỬ LẠI' ---
                                var tryAgainBtn = document.querySelector('.button-incorrect');
                                if (!tryAgainBtn) {
                                    var spans = document.querySelectorAll('span');
                                    for(var i=0; i < spans.length; i++) {
                                        if(spans[i].textContent.trim() === 'Thử lại') {
                                            tryAgainBtn = spans[i].closest('.button') || spans[i];
                                            break;
                                        }
                                    }
                                }
                                if (tryAgainBtn && tryAgainBtn.offsetParent !== null) {
                                    window.guessIndex = (window.guessIndex || 0) + 1; 
                                    tryAgainBtn.click(); 
                                    var innerSpan = tryAgainBtn.querySelector('span');
                                    if (innerSpan) innerSpan.click();
                                    try { tryAgainBtn.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                    return 'CLICKED_TRY_AGAIN';
                                }

                                // --- ƯU TIÊN 5: CHỌN ĐÁP ÁN TRẮC NGHIỆM ---
                                var choices = document.querySelectorAll('.learn-distractor');
                                if (choices.length > 0 && choices[0].offsetParent !== null && (!settingsMenu || settingsMenu.offsetParent === null)) {
                                    window.guessIndex = window.guessIndex || 0;
                                    var safeIndex = window.guessIndex % choices.length; 
                                    var targetChoice = choices[safeIndex];
                                    
                                    targetChoice.click();
                                    try { targetChoice.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                    return 'GUESSED_OPTION_' + safeIndex;
                                }

                                // --- ƯU TIÊN 6: MỞ CÀI ĐẶT (CHỈ KHI ĐANG Ở PHẦN HỌC TỪ) ---
                                var speakMode = document.querySelector('ec-speak-mode');
                                var isSpeaking = speakMode && !speakMode.classList.contains('d-none');
                                var isLearnMode = document.querySelector('.learn-mode') !== null;
                                
                                if (isLearnMode && v.paused && v.currentTime >= 0.5 && choices.length === 0 && !isSpeaking && (!micBtn || micBtn.offsetParent === null)) {
                                    var gearIcon = document.querySelector('.activity-settings-button');
                                    if (gearIcon && gearIcon.offsetParent !== null) {
                                        gearIcon.click();
                                        try { gearIcon.dispatchEvent(new MouseEvent('click', { bubbles: true })); } catch(e) {}
                                        return 'OPENED_SETTINGS';
                                    }
                                }

                                // --- QUẢN LÝ VIDEO VÀ ĐÓNG TAB ---
                                if (speakBtnCompleted) {
                                    return 'ENDED'; // Nếu phần Nói đã hiện tick xanh (completed) thì đóng ngay cho nóng
                                }

                                if (v.paused && v.currentTime < 0.5) {
                                    v.play();
                                    return 'TRYING_TO_PLAY';
                                }
                                
                                if (v.ended || (v.duration > 0 && v.currentTime >= v.duration - 1)) {
                                    if (!speakMenuBtn || speakBtnCompleted) {
                                        return 'ENDED';
                                    }
                                }
                                
                                // Nếu bị dừng bất thường (nhưng không phải đang thu âm hay thi Nói) thì ép chạy
                                if (v.paused && !isSpeaking && !window.micClicked) {
                                    v.play();
                                }
                                
                                return 'PLAYING';
                            ";
                            
                            string status = js.ExecuteScript(checkScript)?.ToString();

                            // ==========================================
                            // XỬ LÝ TRẠNG THÁI DO TRÌNH DUYỆT BÁO VỀ
                            // ==========================================
                            if (status == "NO_VIDEO")
                            {
                                tabErrorCount[handle]++;
                                if (tabErrorCount[handle] > 5) 
                                {
                                    Console.WriteLine($"[Bỏ qua] Video {videoId} bị lỗi không tải được thẻ video.");
                                    status = "CLOSE_TAB"; 
                                }
                            }
                            else if (status == "CLICKED_CONTINUE")
                            {
                                Console.WriteLine($"[{videoId}] Đã hoàn thành một phần, bấm 'Tiếp Tục'...");
                            }
                            else if (status == "CLICKED_START_SPEAK_PHASE")
                            {
                                Console.WriteLine($"[{videoId}] Bấm 'Nói X dòng' để bắt đầu phần luyện Micro...");
                            }
                            else if (status == "CLICKED_SPEAK_MENU")
                            {
                                Console.WriteLine($"[{videoId}] Đã học từ xong, tự động chọn Menu 'Nói'...");
                            }
                            else if (status == "STARTED_MIC")
                            {
                                Console.WriteLine($"[{videoId}] Đã bật Micro. Quá trình thu âm tĩnh lặng bắt đầu...");
                            }
                            else if (status == "RECORDING")
                            {
                                // Đang ghi âm (Micro đã ẩn)
                            }
                            else if (status == "WAITING_FOR_SCORE")
                            {
                                // Đang chờ bảng điểm
                            }
                            else if (status == "CLICKED_SKIP_LINE")
                            {
                                Console.WriteLine($"[{videoId}] Đã chấm điểm xong! Bấm chuyển câu...");
                            }
                            else if (status == "ENDED")
                            {
                                Console.WriteLine($"[{videoId}] [THÀNH CÔNG] Đã nhai gọn toàn bộ video!");
                                status = "CLOSE_TAB";
                            }

                            // Nếu video đã xong (hoặc lỗi), đóng tab này và mở link thay thế
                            if (status == "CLOSE_TAB")
                            {
                                OpenNextLink();
                                
                                driver.SwitchTo().Window(handle);
                                driver.Close();
                                
                                activeTabs.Remove(handle);
                                tabErrorCount.Remove(handle);
                            }
                        }
                        catch (Exception)
                        {
                            // Bỏ qua lỗi vặt (như tab chưa load kịp) để tránh gián đoạn các tab khác
                        }
                    }
                }

                Console.WriteLine("\n=== HOÀN THÀNH: ĐÃ CÀY NÁT TẤT CẢ CÁC LINK TRONG DANH SÁCH! ===");
                Console.WriteLine("Nhấn phím bất kỳ để đóng trình duyệt...");
                Console.ReadKey();
                
                try { driver.Quit(); } catch { } 
            }
        }
    }
}