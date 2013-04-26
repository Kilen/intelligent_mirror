using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace DressRoom
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        bool isWindowsClosing = false;
        bool is_wardrobe_opened = false;
        bool isReady = false;
        bool gesture_lock = false;
        Canvas wardrobe = null;
        string cur_clothes_type = "";
        Image cur_clothes = null;

        const int MaxSkeletonTrackingCount = 6;

        const int HoverDuration = 1000;
        bool hand_entering_button = true;
        Button hovering_button = null;
        Image hovering_clothes = null;
        DateTime start_time;
        

        Skeleton[] allSkeletons = new Skeleton[MaxSkeletonTrackingCount];
        Skeleton cur_skeleton = null;
        KinectSensor kinect;

        const int shirt_num = 6;
        Image[] all_shirts = new Image[shirt_num];
        const int trouser_num = 1;
        Image[] all_trousers = new Image[trouser_num];

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            startKinect();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            stopKinect();
        }

        private void startKinect()
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                kinect = KinectSensor.KinectSensors[0];
                kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                var parameters = new TransformSmoothParameters
                {
                    Smoothing = 0.5f,
                    Correction = 0.5f,
                    Prediction = 0.5f,
                    JitterRadius = 0.05f,
                    MaxDeviationRadius = 0.04f
                };

                kinect.SkeletonStream.Enable(parameters);

                kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinect_AllFramesReady);
                kinect.Start();
            }
            else
            {
                MessageBox.Show("no kinect founded");
            }
        }


        private void stopKinect()
        {
            if (kinect != null && kinect.Status == KinectStatus.Connected)
            {
                kinect.Stop();
            }
        }


        void kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                byte[] pixels = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);

                int stride = colorFrame.Width * 4;

                camera.Source = BitmapSource.Create(colorFrame.Width, colorFrame.Height, 96, 96,
                    PixelFormats.Bgr32, null, pixels, stride);
            }

            cur_skeleton = GetFirstSkeleton(e);
            if (cur_skeleton == null) return;
            check_hand_gesture();
            if (cur_clothes != null) adjust_clothes_position(cur_clothes);

        }

        private void check_hand_gesture()
        {
            check_pressing_button();
            check_taking_clothes_off();
            show_hands();
        }

        private void check_pressing_button()
        {
            if (hand_hovering_button() && horvering_duration_met_certain_conditon())
            {
                successful_state();
                if (hovering_button != null)
                {
                    press_button(hovering_button);
                }
                else
                {
                    press_clothes_button();
                }
            }
            else
            {
                release_button();
            }
        }

        private void check_taking_clothes_off()
        {
            if (is_in_clothes() && are_hands_overlapping())
            {
                successful_state();
                take_off_clothes();
            }
        }

        private void take_off_clothes()
        {
            MainCanvas.Children.Remove(cur_clothes);
            cur_clothes = null;
        }

        private void shirts_button_Click(object sender, RoutedEventArgs e)
        {
            empty_or_fill_wardrobe("shirt");
        }

        private void trousers_button_Click(object sender, RoutedEventArgs e)
        {
            empty_or_fill_wardrobe("trouser");
        }

        private void empty_or_fill_wardrobe(string type)
        {
            MainCanvas.Children.Remove(wardrobe);
            wardrobe = null;
            if (cur_clothes_type != type)
            {
                cur_clothes_type = type;
                wardrobe = create_wardrobe();
                load_shirts_or_trousers(type);
            }
            else
            {
                cur_clothes_type = "";
            }
        }

        private void load_shirts_or_trousers(string type)
        {
            if (type == "shirt")
            {
                load_shirts(wardrobe);
            }
            else
            {
                load_trousers(wardrobe);
            }
        }

        private void press_button(Button button)
        {
            if (gesture_lock == false)
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                gesture_lock = true;
            }
        }

        private void press_clothes_button()
        {
            if (gesture_lock == false)
            {
                put_on_clothes();
                gesture_lock = true;
            }
        }

        private void put_on_clothes()
        {
            if(is_in_clothes()) take_off_clothes();
            cur_clothes = clone_clothes(hovering_clothes);
            adjust_clothes_position(cur_clothes);
        }

        private void release_button()
        {
            gesture_lock = false;
            hovering_button = null;
            hovering_clothes = null;
        }

        private bool hand_hovering_button()
        {

            ColorImagePoint left_hand = getJoint(JointType.HandLeft);
            ColorImagePoint right_hand = getJoint(JointType.HandRight);
            if (hand_in_button(left_hand) || hand_in_button(right_hand))
            {
                preparing_state();
                if (hand_entering_button)
                {
                    start_time = DateTime.Now;
                    hand_entering_button = false;
                }
                return true;
            }
            else
            {
                normal_state();
                hand_entering_button = true;
                return false;
            }
        }

        private void show_hands()
        {
            ColorImagePoint left_hand = getJoint(JointType.HandLeft);
            ColorImagePoint right_hand = getJoint(JointType.HandRight);
            show_image(leftEllipse);
            show_image(rightEllipse);
            adjustHandPosition(leftEllipse, left_hand, 40);
            adjustHandPosition(rightEllipse, right_hand, 40);
        }

        private void hide_hands()
        {
            hide_image(leftEllipse);
            hide_image(rightEllipse);
        }

        private void successful_state()
        {
            set_ellipses_color(Colors.Green);
        }

        private void normal_state()
        {
            set_ellipses_color(Colors.Blue);
        }

        private void preparing_state()
        {
            set_ellipses_color(Colors.Yellow);
        }

        private void set_ellipses_color(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = color;
            leftEllipse.Fill = brush;
            rightEllipse.Fill = brush;
        }


        private void show_image(Ellipse ellipse)
        {
            ellipse.Visibility = System.Windows.Visibility.Visible;
        }

        private void hide_image(Ellipse ellipse)
        {
            ellipse.Visibility = System.Windows.Visibility.Hidden;
        }


        private bool horvering_duration_met_certain_conditon()
        {
            TimeSpan duration = DateTime.Now - start_time;
            if (duration.TotalMilliseconds >= HoverDuration)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool hand_in_button(ColorImagePoint point)
        {
            Button[] all_buttons = return_available_buttons();
            Image[] all_clothes = return_available_clothes();
            for (int i = 0; i < all_buttons.Length; i++)
            {
                if(in_button(point, all_buttons[i])) return true;
            }
            for (int i = 0; all_clothes != null && i < all_clothes.Length; i++)
            {
                if(in_clothes(point, all_clothes[i])) return true;
            }
            return false;
        }

        private Button[] return_available_buttons()
        {
            Button[] all_buttons = new Button[2];
            all_buttons[0] = shirts_button;
            all_buttons[1] = trousers_button;
            return all_buttons;
        }

        private Image[] return_available_clothes()
        {
            if (cur_clothes_type == "shirt")
            {
                return all_shirts;
            }
            else if (cur_clothes_type == "trouser")
            {
                return all_trousers;
            }
            else
            {
                return null;
            }
        }

        private bool in_button(ColorImagePoint point, Button button)
        {
            if ((point.X >= Canvas.GetLeft(button) && point.X <= Canvas.GetLeft(button) + button.Width) &&
                (point.Y >= Canvas.GetTop(button) && point.Y <= Canvas.GetTop(button) + button.Height))
            {
                hovering_button = button;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool in_clothes(ColorImagePoint point, Image image)
        {
            if ((point.X >= Canvas.GetLeft(image) && point.X <= Canvas.GetLeft(image) + image.Width) &&
                (point.Y >= Canvas.GetTop(image) && point.Y <= Canvas.GetTop(image) + image.Height))
            {
                hovering_clothes = image;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool is_wardrobe_empty()
        {
            return wardrobe == null;
        }

        Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null;
                }

                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                Skeleton first_skeleton = (from s in allSkeletons
                                  where s.TrackingState == SkeletonTrackingState.Tracked
                                  select s).FirstOrDefault();
                return first_skeleton;
            }
        }

        bool is_in_clothes()
        {
            return cur_clothes != null;
        }

        bool are_hands_overlapping()
        {
            ColorImagePoint p1 = getJoint(JointType.HandLeft);
            ColorImagePoint p2 = getJoint(JointType.HandRight);
            return (Math.Abs(p1.X - p2.X) <= 10) && (Math.Abs(p1.Y - p2.Y) <= 10);
        }

        void mappingSkeleton2CameraCoordinate(AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null || kinect == null)
                {
                    return;
                }

                ColorImagePoint left_hand = getJoint(JointType.HandLeft);
                ColorImagePoint right_hand = getJoint(JointType.HandRight);
                ColorImagePoint shoulder_left = getJoint(JointType.ShoulderLeft);
                ColorImagePoint shoulder_right = getJoint(JointType.ShoulderRight);
                ColorImagePoint shoulder_center = getJoint(JointType.ShoulderCenter);
                ColorImagePoint hip_center = getJoint(JointType.HipCenter);

                Double shoulder_width = Math.Abs(shoulder_left.X - shoulder_right.X);
                Double body_height = Math.Abs(shoulder_center.Y - hip_center.Y);
                //adjust_clothes_position(shirt, shoulder_center, shoulder_width, body_height);
                adjustHandPosition(leftEllipse, left_hand, shoulder_width / 2);
                adjustHandPosition(rightEllipse, right_hand, shoulder_width / 2);
            }

        }

        private ColorImagePoint getJoint(JointType joint)
        {
            ColorImagePoint p = kinect.MapSkeletonPointToColor(cur_skeleton.Joints[joint].Position, ColorImageFormat.RgbResolution640x480Fps30);
            double x_scale_rate = camera.Width / 640;
            double y_scale_rate = camera.Height / 480;
            p.X = (int)((double)p.X * x_scale_rate);
            p.Y = (int)((double)p.Y * y_scale_rate);
            return p;
        }

        private void adjustHandPosition(FrameworkElement element, ColorImagePoint hand, Double half_shoulder_width)
        {
            element.Width = half_shoulder_width;
            element.Height = half_shoulder_width;
            Canvas.SetLeft(element, hand.X - element.Width / 2);
            Canvas.SetTop(element, hand.Y - element.Height / 2);
        }

        private void adjust_clothes_position(Image clothes)
        {
            ColorImagePoint left_hand = getJoint(JointType.HandLeft);
            ColorImagePoint right_hand = getJoint(JointType.HandRight);
            ColorImagePoint shoulder_left = getJoint(JointType.ShoulderLeft);
            ColorImagePoint shoulder_right = getJoint(JointType.ShoulderRight);
            ColorImagePoint shoulder_center = getJoint(JointType.ShoulderCenter);
            ColorImagePoint hip_center = getJoint(JointType.HipCenter);
            Double shoulder_width = Math.Abs(shoulder_left.X - shoulder_right.X);
            Double body_height = Math.Abs(shoulder_center.Y - hip_center.Y);
            clothes.Width = shoulder_width * 2.7;
            clothes.Height = body_height * 2;
            Canvas.SetLeft(clothes, shoulder_center.X - clothes.Width / 2);
            Canvas.SetTop(clothes, shoulder_center.Y - shoulder_width / 5);
        }

       

        private Canvas create_wardrobe()
        {
            Canvas wardrobe = new Canvas();
            wardrobe.Name = "gird";
            wardrobe.Width = camera.Width;
            wardrobe.Height = camera.Height;
            Canvas.SetLeft(wardrobe, 0);
            Canvas.SetTop(wardrobe, 0);
            
            MainCanvas.Children.Add(wardrobe);
            return wardrobe;
        }

        private void load_shirts(Canvas wardrobe)
        {
            for (int i = 0; i < 6; i++)
            {
                add_clothes(wardrobe, "shirt", i);
            }
        }

        private void load_trousers(Canvas wardrobe)
        {
            for (int i = 0; i < 1; i++)
            {
                add_clothes(wardrobe, "trouser", i);
            }
        }

        private void add_clothes(Canvas wardrobe, string type, int i)
        {
            Image image = new Image();
            image.Name = type + "_" + i;
            image.Height = 90;
            image.Width = 80;
            image.Source = image_source(type, i);
            Canvas.SetLeft(image, camera.Width - image.Width - 30);
            Canvas.SetTop(image, 20 + i * image.Height);
            wardrobe.Children.Add(image);
            save_shirts_or_trousers(image, i);
        }

        private Image clone_clothes(Image target)
        {
            Image clone = new Image();
            clone.Name = target.Name;
            clone.Height = target.Height;
            clone.Width = target.Width;
            clone.Source = target.Source;
            MainCanvas.Children.Add(clone);
            return clone;
        }
        private void save_shirts_or_trousers(Image image, int i)
        {
            if (cur_clothes_type == "shirt")
            {
                all_shirts[i] = image;
            }
            else 
            {
                all_trousers[i] = image;
            }
        }

        private BitmapImage image_source(string type, int i)
        {
            string uri = "images/" + type + "s/" + type + "_" + i + ".png";
            return new BitmapImage(new Uri(uri, UriKind.Relative));
        }


        

        
    }
}
