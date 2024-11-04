"""
SIMPLE CLIENT FOR SOCKET CLIENT
"""

import socket
import json

import pyrealsense2 as rs
import numpy as np
import cv2
from MediaPipe import MediaPipe

HOST = "127.0.0.1"  # The server's hostname or IP address
PORT = 8080            # The port used by the server

def receive(sock):
    data = sock.recv(1024)
    data = data.decode('utf-8')
    msg = json.loads(data)
    print("Received: ", msg)
    return msg

def send(sock, msg):
	data = json.dumps(msg)
	sock.sendall(data.encode('utf-8'))
	print("Sent: ", msg)


#Realsense Setup
# Configure depth and color streams
pipeline = rs.pipeline()
config = rs.config()

# Get device product line for setting a supporting resolution
pipeline_wrapper = rs.pipeline_wrapper(pipeline)
pipeline_profile = config.resolve(pipeline_wrapper)
device = pipeline_profile.get_device()
device_product_line = str(device.get_info(rs.camera_info.product_line))

found_rgb = False
for s in device.sensors:
    if s.get_info(rs.camera_info.name) == 'RGB Camera':
        found_rgb = True
        break
if not found_rgb:
    print("The demo requires Depth camera with Color sensor")
    exit(0)

config.enable_stream(rs.stream.depth, 640, 480, rs.format.z16, 30)
config.enable_stream(rs.stream.color, 640, 480, rs.format.bgr8, 30)

#Setup ARuco detector
arucoDict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_6X6_250)
arucoParams = cv2.aruco.DetectorParameters()
arucoDetector = cv2.aruco.ArucoDetector(arucoDict,arucoParams)

# Start streaming
pipeline.start(config)

#Set up dictionary of detected ArUco codes
detected_aruco = {}

#Set up transformation matrix to convert realsense space to unity space
transformation_matrix = np.array([[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]])

calibrated = False

mp = MediaPipe()

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
	sock.connect((HOST, PORT))
	try:
		while True:
			try:
				


				# Wait for a coherent pair of frames: depth and color
				frames = pipeline.wait_for_frames()
				depth_frame = frames.get_depth_frame()
				color_frame = frames.get_color_frame()
				if not depth_frame or not color_frame:
					continue

				# Convert images to numpy arrays
				depth_image = np.asanyarray(depth_frame.get_data())
				color_image = np.asanyarray(color_frame.get_data())

				corners, ids, rejected = arucoDetector.detectMarkers(color_image)
				color_image = cv2.aruco.drawDetectedMarkers(color_image, corners, ids)

				# Apply colormap on depth image (image must be converted to 8-bit per pixel first)
				depth_colormap = cv2.applyColorMap(cv2.convertScaleAbs(depth_image, alpha=0.03), cv2.COLORMAP_JET)

				depth_colormap_dim = depth_colormap.shape
				color_colormap_dim = color_image.shape

				# If depth and color resolutions are different, resize color image to match depth image for display
				if depth_colormap_dim != color_colormap_dim:
					resized_color_image = cv2.resize(color_image, dsize=(depth_colormap_dim[1], depth_colormap_dim[0]), interpolation=cv2.INTER_AREA)
					images = np.hstack((resized_color_image, depth_colormap))
				else:
					images = np.hstack((color_image, depth_colormap))

				# Show images
				cv2.namedWindow('RealSense', cv2.WINDOW_AUTOSIZE)
				cv2.imshow('RealSense', images)
				cv2.waitKey(1)

				#Recive message first and then do calculations
				if(not calibrated):
					msg = receive(sock)
					#Note: 3D coordinate becomes 0 if the codes are too close to the depth sensor
					#Dictionary of ids
					codes = {}
					if len(corners) > 0:
						codes = {}
						ind = 0
						#Loop thorugh all seen ArUco codes
						for id in ids:
							#Get corner of the current ArUco code
							corner = corners[ind][0]
							x = 0
							y = 0
							for point in corner:
								x += point[0]
								y += point[1]
							#Calculate the center of one AruCo point
							centerX = x/4
							centerY = y/4
							ind = ind + 1

							#Add the center of the AruCo point to the dictionary of 3D distances
							depth = depth_frame.get_distance(centerX,centerY)
							depth_intrinsics = depth_frame.profile.as_video_stream_profile().intrinsics
							threeDPoint = rs.rs2_deproject_pixel_to_point(depth_intrinsics, [centerX,centerY], depth)
							codes[int(id[0])] = threeDPoint

						#Add codes to the aruco dictionary if they are not already there
						for key in codes:
							detected_aruco[key] = codes[key]
						#print(codes)

					#Using numpy least squares to calculate the transformation matrix
					#If there are more than 2 codes, calculate the transformation matrix
					if len(detected_aruco) > 2 and len(msg['ids'].split(" ")) > 2:
						unity_points = {}
						for x in range(len(msg['ids'].split(" "))):
							unity_points[int(msg['ids'].split(" ")[x])] = np.array([float(msg['xs'].split(" ")[x]),float(msg['ys'].split(" ")[x]),float(msg['zs'].split(" ")[x]),1])
						
						unity_positions = []
						threeD_positions = []

						for id in detected_aruco:
							if id in unity_points:
								unity_positions.append(unity_points[id])
								threeD_positions.append(detected_aruco[id])

						unity_positions = np.array(unity_positions)
						threeD_positions = np.array(threeD_positions)
						ones = np.ones((threeD_positions.shape[0], 1))
						threeD_positions = np.hstack((threeD_positions, ones))

						

						#print(unity_positions)
						#print(threeD_positions)

						#Get the transformation matrix
						T, residuals, rank, s = np.linalg.lstsq(threeD_positions, unity_positions, rcond=None)
						transformation_matrix = np.vstack((T.T, [0,0,0,1]))
						print(transformation_matrix)

					#Use the transformation matrix to calculate the position of the detected ArUco codes
					#Send back the calculated positions for the ArUco codes
					ids = ""
					xs = ""
					ys = ""
					zs = ""
					for id in detected_aruco:
						threeDPoint = detected_aruco[id] + [1]
						unityPoint = transformation_matrix @ threeDPoint
						ids = ids + str(id) + " "
						xs = xs + str(unityPoint[0]) + " "
						ys = ys + str(unityPoint[1]) + " "
						zs = zs + str(unityPoint[2]) + " "
					calibrated = True
				else:
					#Conduct normal position sending of seen arucos
					detected_aruco = {}
					#Note: 3D coordinate becomes 0 if the codes are too close to the depth sensor
					#Dictionary of ids
					codes = {}
					if len(corners) > 0:
						codes = {}
						ind = 0
						#Loop thorugh all seen ArUco codes
						for id in ids:
							#Get corner of the current ArUco code
							corner = corners[ind][0]
							x = 0
							y = 0
							for point in corner:
								x += point[0]
								y += point[1]
							#Calculate the center of one AruCo point
							centerX = x/4
							centerY = y/4
							ind = ind + 1

							#Add the center of the AruCo point to the dictionary of 3D distances
							depth = depth_frame.get_distance(centerX,centerY)
							depth_intrinsics = depth_frame.profile.as_video_stream_profile().intrinsics
							threeDPoint = rs.rs2_deproject_pixel_to_point(depth_intrinsics, [centerX,centerY], depth)
							codes[int(id[0])] = threeDPoint

						#Add codes to the aruco dictionary if they are not already there
						for key in codes:
							detected_aruco[key] = codes[key]
						#print(codes)

					#Use the transformation matrix to calculate the position of the detected ArUco codes
					#Send back the calculated positions for the ArUco codes
					ids = ""
					xs = ""
					ys = ""
					zs = ""
					for id in detected_aruco:
						threeDPoint = detected_aruco[id] + [1]
						unityPoint = transformation_matrix @ threeDPoint
						ids = ids + str(id) + " "
						xs = xs + str(unityPoint[0]) + " "
						ys = ys + str(unityPoint[1]) + " "
						zs = zs + str(unityPoint[2]) + " "

					#Now read media pipe data
					# Detect skeleton and send it to Unity
					color_image = np.asanyarray(color_frame.get_data())
					cropped_image = color_image[0:480, 480:640]
					#Changed color image to cropped image and cropped image to cropped image for testing 480, 0, 160, 480
					detection_results = mp.detect(cropped_image)	
					color_image = mp.draw_landmarks_on_image(cropped_image, detection_results)
					skeleton_data = mp.skeleton(cropped_image, detection_results, depth_frame)

					if skeleton_data is not None:
						L=np.array([skeleton_data.get('LHand_x'),skeleton_data.get('LHand_y'),skeleton_data.get('LHand_z'),1])
						R=np.array([skeleton_data.get('RHand_x'),skeleton_data.get('RHand_y'),skeleton_data.get('RHand_z'),1])
						H=np.array([skeleton_data.get('Head_x'),skeleton_data.get('Head_y'),skeleton_data.get('Head_z'),1])

						# L = np.array([skeleton_data.get('LHand_x'), -skeleton_data.get('LHand_y'), skeleton_data.get('LHand_z'), 1])
						# R = np.array([skeleton_data.get('RHand_x'), -skeleton_data.get('RHand_y'), skeleton_data.get('RHand_z'), 1])
						# H = np.array([skeleton_data.get('Head_x'), -skeleton_data.get('Head_y'), skeleton_data.get('Head_z'), 1])
						
						
						#transformation_matrix = np.array([[-0.42868,0.24386064,-0.25474547,-1.1521421],[-0.20920509,-0.69774109,0.91796461,-0.18616567],[1.06923961,-0.05722082,-0.58019261,0.3841163],[0,0,0,1]])
						print("+++++")
						# Lhand=(transformation_matrix @ L)
						# Rhand=(transformation_matrix @ R)
						Lhand=(transformation_matrix @ L)
						Rhand=(transformation_matrix @ R)
						Head=(transformation_matrix @ H)
						print(Lhand)
						print(Rhand)
						print(Head)

						#Add the skeleton data to the message
						ids = ids + "500 " + "501 " + "502 "
						xs = xs + str(Lhand[0]) + " " + str(Rhand[0]) + " " + str(Head[0]) + " "
						ys = ys + str(Lhand[1]) + " " + str(Rhand[1]) + " " + str(Head[1]) + " "
						zs = zs + str(Lhand[2]) + " " + str(Rhand[2]) + " " + str(Head[2]) + " "

					#Trim the last space
					ids = ids[:-1]
					xs = xs[:-1]
					ys = ys[:-1]
					zs = zs[:-1]
				
				#Send the calculated positions back to the server
				msg = {"ids":ids,"xs":xs,"ys":ys,"zs":zs}
				send(sock, msg)
			except KeyboardInterrupt:
				exit()
			except Exception as e:
				#If there is an error, print and then pass
				print(e)
				pass
	finally:
		pipeline.stop()