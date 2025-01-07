CREATE TABLE IF NOT EXISTS `profiles` (
  `user_name` varchar(255) NOT NULL,
  `lastactivitydate` datetime DEFAULT NULL,
  `propertyname` varchar(255) NOT NULL,
  `propertyvalue` text DEFAULT NULL,
  PRIMARY KEY (`user_name`,`propertyname`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `roles` (
  `role_id` int(11) NOT NULL AUTO_INCREMENT,
  `role_name` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`role_id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `users` (
  `user_id` int(11) NOT NULL AUTO_INCREMENT,
  `user_name` varchar(50) NOT NULL,
  `user_password` binary(60) NOT NULL,
  `user_email` varchar(255) DEFAULT NULL,
  `user_regdate` datetime DEFAULT NULL,
  `user_last_login` datetime DEFAULT NULL,
  `user_last_activity` datetime DEFAULT NULL,
  `user_last_password_changed` datetime DEFAULT NULL,
  `user_last_lockout` datetime DEFAULT NULL,
  `user_password_question` text DEFAULT NULL,
  `user_password_answer` text DEFAULT NULL,
  `user_approved` int(11) DEFAULT NULL,
  `user_locked` int(11) DEFAULT NULL,
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=1 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

CREATE TABLE IF NOT EXISTS `user_roles` (
  `user_id` int(11) DEFAULT NULL,
  `role_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;
