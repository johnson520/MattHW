
#undef UNICODE

#include "stdafx.h"
#include "stdio.h"
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#include "winsock2.h"
#include "ws2tcpip.h"

#pragma comment(lib,"ws2_32.lib") // Winsock Library

bool splitUrl(char *in, char **host, char **path) {

	if (strstr(in, "https://") == in) {
		printf("We only handle http: not https:\n");
		return false;
	}

	if (strstr(in, "http://") == in) {
		*host = in + strlen("http://");
	}
	else {
		*host = in;
	}

	*path = strchr(*host, '/');

	if (*path != NULL) {
		size_t hostLen = *path - *host;
		char *p = (char *) malloc(hostLen + 1);
		strncpy_s(p, hostLen + 1, *host, hostLen);
		*host = p;
	}
	else {
		*path = "/";
	}

	return true;
}

static char *HttpOk = "HTTP/1.1 200 OK";

int main(int argc, char *argv []) {

	if (argc != 2) {
		printf("Usage: %s hostname", argv[0]);
		return 1;
	}

	char *hostName, *path;
	if (!splitUrl(argv[1], &hostName, &path)) {
		return 1;
	}

	WSADATA wsa;
	struct addrinfo *result = NULL;
	SOCKET mySocket;

	printf("Initializing Windows sockets (Winsock)... ");
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
		printf("Failed with error: %d\n", WSAGetLastError());
		return 1;
	}
	printf("initialized.\n");

	printf("Resolving host name %s... ", hostName);
	DWORD dwRetval = getaddrinfo(hostName, NULL, NULL, &result);
	if (dwRetval != 0) {
		printf("getaddrinfo failed with error: %d\n", dwRetval);
		WSACleanup();
		return 1;
	}

	if (result == NULL) {
		printf("getaddrinfo succeeded but returned no results\n");
		WSACleanup();
		return 1;
	}

	if (result->ai_family != AF_INET) {
		printf("getaddrinfo succeeded but family is not IPv4\n");
		WSACleanup();
		return 1;
	}

	struct sockaddr_in *server = (struct sockaddr_in *)result->ai_addr;
	server->sin_port = htons(80);

	printf("resolved to IPv4 address %s\n", inet_ntoa(server->sin_addr));

	printf("Creating socket... ");
	if ((mySocket = socket(result->ai_family, result->ai_socktype, result->ai_protocol)) == INVALID_SOCKET) {
		printf("Failed with error: %d\n", WSAGetLastError());
		WSACleanup();
		return 1;
	}
	printf("created.\n");

	printf("Connecting to server... ");
	if (connect(mySocket, (struct sockaddr *)server, sizeof(*server)) == SOCKET_ERROR) {
		printf("Failed with error: %d\n", WSAGetLastError());
		closesocket(mySocket);
		WSACleanup();
		return 1;
	}
	printf("connected.\n");

	//	format a GET header
	char header[256];
	sprintf_s(header, "GET %s HTTP/1.1\r\nHost: %s\r\nConnection: close\r\n\r\n", path, hostName);

	// send the GET header
	printf("Sending GET header... ");
	int numBytesSent = send(mySocket, header, strlen(header), 0);
	if (numBytesSent == SOCKET_ERROR) {
		printf("Failed with error: %d\n", WSAGetLastError());
		closesocket(mySocket);
		WSACleanup();
		return 1;
	}

	printf("sent %d header bytes.\n", numBytesSent);

	printf("Receiving data...\n");

	char recvbuf[1024];
	int numBytesReceived, totalReceived = 0;
	
	size_t bigBufSize = 10000;
	char *allBytes = (char *)malloc(bigBufSize);

	do {
		numBytesReceived = recv(mySocket, recvbuf, sizeof(recvbuf), 0);

		if (numBytesReceived > 0) {
			printf("%d bytes received\n", numBytesReceived);

			if (bigBufSize - totalReceived < (size_t)numBytesReceived) {
				bigBufSize += 10000;
				allBytes = (char *) realloc(allBytes, bigBufSize + 10000);
			}

			memcpy_s(&allBytes[totalReceived], bigBufSize - totalReceived, recvbuf, numBytesReceived);
			totalReceived += numBytesReceived;
		}
		else if (numBytesReceived == 0) {
			printf("Connection closed\n");
		}
		else {
			printf("recv failed: %d\n", WSAGetLastError());
			closesocket(mySocket);
			WSACleanup();
			return 1;
		}

	} while (numBytesReceived > 0);

	printf("%d total bytes received GETting %s\n", totalReceived, argv[1]);

	//	make sure we have room for one more byte
	if (bigBufSize - totalReceived < 1) {
		bigBufSize += 1;
		allBytes = (char *) realloc(allBytes, bigBufSize + 10000);
	}
	allBytes[totalReceived] = '\0';

	char *endOfHeader = strstr(allBytes, "\r\n\r\n");
	if (endOfHeader != NULL) {
		size_t headerLength = (endOfHeader - allBytes) + 2;
		char *receivedHeader = (char *)malloc(headerLength + 1);
		memcpy_s(receivedHeader, headerLength + 1, allBytes, headerLength);
		receivedHeader[headerLength] = '\0';

		printf("%d bytes of received header:\n", headerLength);
		fputs(receivedHeader, stdout);

		if (strncmp(receivedHeader, HttpOk, strlen(HttpOk)) != 0) {
			printf("\n!! GET did return HTTP status 200 OK\n");
		}
	}

	//printf("The received document:\n\n");
	//fputs(allBytes, stdout);
	//printf("\n\n");

	closesocket(mySocket);
	WSACleanup();
	return 0;
}
