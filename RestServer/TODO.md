﻿# FindFm - TODO

- Limite de tempo para as mídias que forem enviadas, 15 segundos
	- Por causa de problemas de direitos autorais e/ou storage
- Termos de Uso: Se a música for entrar na rádio, tem que ser de autoria própria
- Requisito não funcional: Limite de tamanho consumido no DB por usuário (disponibilidade p/ outros e segurança)

Paginação com Limit e Skip do MongoDb via QueryParameter padronizado (pode ser limit e skip mesmo e o app padroniza pra cada tipo quanto ele quer ali)

Para postar mídias: 
1-envia a musica/video primeiro para um local temporario
2-dps anexa o id desse upload no json do post, para linkar os dois
