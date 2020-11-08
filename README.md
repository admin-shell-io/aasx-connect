# AASX Connector

AASX Connector enables a simple publish and subscribe via REST.

AASX Server can connect to an AASX Connector and an AASX Connector can connect to another AASX Connector.
By this a tree of connectors can be built.

The REST communication is company proxy/firewall friendly. It uses only an inside-out communication.

A ready to use connector is running on http://admin-shell-io.com:52000. AASX Server connects to this by the option "--connect".
This used for I40 Language and for Publish/Subscribe in AASX Server.

