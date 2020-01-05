CNF ESB Project

ESB is used to insulate the data-exchanging between client apps and server apps.

There are two projects in this solution:
1. cnf.esb.web
  This is the main project of ESB, in fact, we need deploy this project into a docker container just only.
  In the "data" folder of this project, "esb.db" file is a database (SQLite) which is used to store all configurations and executing logs.
2. cnf.esb.testApi
  This is a fake server app that is used for testing ESB features. You needn't deploy it on production.

Main features of ESB
1. Management
(1) To register a client app as a "consumer" in ESB;
(2) To register an API of server app as a "service" in ESB;
(3) To create an "instance" which allow a "consumer" to invoke a "service";
(4) To view executing logs which are recorded when consomer invoked service.
2. Deamon
(1) Caller developers can use "https://[esb_host:port]/api/help/[instance_id]" to display the contract of which consumer invoke service and the response from service;
(2) In client app, POST data to "https://[esb_host:port]/api/invoke/[instance_id]" to cosume the API.

